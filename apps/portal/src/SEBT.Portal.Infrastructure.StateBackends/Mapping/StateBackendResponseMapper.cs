using System.Globalization;
using System.Text.Json;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Maps a state backend's raw JSON response into canonical domain types, driven by
/// <see cref="StateBackendResponseMapping"/> and the backend's named enum tables.
///
/// DC-568 spike scope (deliberately capped — see the prototype plan's STOP rule):
///   * Root selection supports ONLY simple dotted property access and <c>[index]</c> element
///     access (e.g. <c>$.resultSets[0]</c>). No JSONPath filters, wildcards, or recursion.
///   * Field mapping targets a CLOSED set of canonical fields (see <see cref="FieldTargets"/>).
///     Coercion is driven by the canonical field's known type: strings copy; dates parse with the
///     field's exact <see cref="FieldMapping.Format"/>; enums resolve through the named
///     <see cref="FieldMapping.Enum"/> table. The only supported "bricks" are from/format/enum.
///   * Disaggregation supports classification (<see cref="DisaggregationRule.Presence"/> /
///     <see cref="DisaggregationRule.ValueInSet"/>), the <see cref="CaseInclusionPredicate.All"/> and
///     <see cref="CaseInclusionPredicate.WhenApprovedOrNotApplicationBased"/> case-inclusion
///     predicates, and grouping application-based records into applications by a single field. The
///     approval-aware predicate reads the mapped canonical <see cref="ApplicationStatus"/> — supply it
///     via an <c>applicationStatus</c> field mapping + enum table (it takes no config parameters).
/// </summary>
internal static class StateBackendResponseMapper
{
    /// <summary>
    /// The closed set of canonical field targets. Each entry names its coercion kind and, for
    /// enum targets, the C# enum type its named table must resolve into. A new canonical target
    /// requires adding an entry here — never reflection over property names.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, FieldTarget> FieldTargets =
        new Dictionary<string, FieldTarget>(StringComparer.Ordinal)
        {
            ["summerEBTCaseID"] = FieldTarget.String((c, v) => c.SummerEBTCaseID = v),
            ["childFirstName"] = FieldTarget.String((c, v) => c.ChildFirstName = v),
            ["childLastName"] = FieldTarget.String((c, v) => c.ChildLastName = v),
            ["applicationId"] = FieldTarget.String((c, v) => c.ApplicationId = v),
            ["ebtCardIssueDate"] = FieldTarget.DateTime((c, v) => c.EbtCardIssueDate = v),
            ["ebtCardStatus"] = FieldTarget.Enum<CardStatus>((c, v) => c.EbtCardStatus = v),
            ["applicationStatus"] = FieldTarget.Enum<ApplicationStatus>((c, v) => c.ApplicationStatus = v),
            ["issuanceType"] = FieldTarget.Enum<IssuanceType>((c, v) => c.IssuanceType = v),
        };

    /// <summary>
    /// Validates every enum table referenced by any response field mapping (fail-loud, at
    /// configuration time): each OUR value must be a real member of the target C# enum, and no
    /// source token may appear under two of OUR values. Throws <see cref="InvalidOperationException"/>
    /// on the first violation.
    /// </summary>
    public static void ValidateEnumTables(StateBackendConfiguration configuration)
    {
        foreach (StateBackendResponseMapping mapping in ResponseMappings(configuration))
        {
            foreach ((string canonicalField, FieldMapping fieldMapping) in mapping.Fields)
            {
                if (fieldMapping.KeywordRules is { } keywordRules)
                {
                    ValidateKeywordRules(canonicalField, keywordRules);
                    continue;
                }

                if (fieldMapping.Enum is not { } tableName)
                {
                    continue;
                }

                FieldTarget target = ResolveTarget(canonicalField);
                if (target.EnumType is null)
                {
                    throw new InvalidOperationException(
                        $"Field '{canonicalField}' references enum table '{tableName}' but is not an enum-typed target.");
                }

                StateBackendEnumTable table = ResolveTable(configuration, tableName);
                BuildTokenLookup(tableName, table, target.EnumType);
            }
        }
    }

    /// <summary>
    /// Maps the selected records into cases and, when disaggregation is configured, splits out
    /// grouped applications and links each application-based case to its application.
    /// </summary>
    public static HouseholdData MapHousehold(JsonElement root, StateBackendConfiguration configuration, StateBackendResponseMapping mapping)
    {
        JsonElement records = SelectPath(root, mapping.Root);
        var household = new HouseholdData();

        if (records.ValueKind != JsonValueKind.Array)
        {
            return household;
        }

        // Fail loud on bad enum tables / keyword rules before mapping any records.
        Dictionary<string, EnumResolver> enumResolvers = BuildEnumResolvers(configuration, mapping);
        Dictionary<string, KeywordRuleResolver> keywordResolvers = BuildKeywordResolvers(mapping);

        StateBackendDisaggregation? disaggregation = mapping.Disaggregation;

        // Group keys, in first-seen order, for the application-based records.
        var applicationKeys = new List<string>();
        var seenApplicationKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (JsonElement record in records.EnumerateArray())
        {
            // Map to canonical first — the inclusion predicate reads the canonical model (e.g.
            // ApplicationStatus), never raw state fields.
            SummerEbtCase summerEbtCase = MapCase(record, mapping.Fields, enumResolvers, keywordResolvers);

            // Without disaggregation, records map 1:1 into a flat case list.
            if (disaggregation is null)
            {
                household.SummerEbtCases.Add(summerEbtCase);
                continue;
            }

            bool isApplicationBased = IsApplicationBased(record, disaggregation);

            // Every application-based record (regardless of case inclusion) belongs to its grouped
            // application: an app-based-but-not-included record is still part of a pending application.
            if (isApplicationBased && GroupKey(record, disaggregation) is { } key)
            {
                summerEbtCase.ApplicationId = key;

                if (seenApplicationKeys.Add(key))
                {
                    applicationKeys.Add(key);
                }
            }

            if (IncludeAsCase(disaggregation.CaseInclusion, isApplicationBased, summerEbtCase.ApplicationStatus))
            {
                household.SummerEbtCases.Add(summerEbtCase);
            }
        }

        foreach (string key in applicationKeys)
        {
            household.Applications.Add(new Application { ApplicationNumber = key });
        }

        return household;
    }

    /// <summary>
    /// Named case-inclusion predicates are a closed vocabulary — each reads only the canonical model,
    /// never raw state fields, so they need no config parameters.
    ///   * <see cref="CaseInclusionPredicate.All"/> includes every record.
    ///   * <see cref="CaseInclusionPredicate.WhenApprovedOrNotApplicationBased"/> includes a record
    ///     when it is not application-based, or when its mapped canonical
    ///     <see cref="ApplicationStatus"/> is <see cref="ApplicationStatus.Approved"/>. An unknown or
    ///     unmapped status is not Approved, so an application-based record with such a status is
    ///     excluded (fail-closed for inclusion).
    /// </summary>
    private static bool IncludeAsCase(
        CaseInclusionPredicate caseInclusion, bool isApplicationBased, ApplicationStatus applicationStatus)
    {
        return caseInclusion switch
        {
            CaseInclusionPredicate.All => true,
            CaseInclusionPredicate.WhenApprovedOrNotApplicationBased =>
                !isApplicationBased || applicationStatus == ApplicationStatus.Approved,
            _ => throw new NotSupportedException(
                $"Case inclusion predicate '{caseInclusion}' is not implemented by the response mapper."),
        };
    }

    private static bool IsApplicationBased(JsonElement record, StateBackendDisaggregation disaggregation)
    {
        string? discriminator = ReadString(record, disaggregation.DiscriminatorField);

        return disaggregation.Rule switch
        {
            DisaggregationRule.Presence => !string.IsNullOrEmpty(discriminator),
            DisaggregationRule.ValueInSet =>
                discriminator is not null
                && disaggregation.ApplicationValues is { } values
                && values.Contains(discriminator, StringComparer.Ordinal),
            _ => throw new NotSupportedException(
                $"Disaggregation rule '{disaggregation.Rule}' is not supported by the response mapper."),
        };
    }

    private static string? GroupKey(JsonElement record, StateBackendDisaggregation disaggregation)
    {
        if (disaggregation.GroupApplicationsBy is not { } groupField)
        {
            return null;
        }

        string? value = ReadString(record, groupField);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string? ReadString(JsonElement record, string property)
    {
        if (record.ValueKind != JsonValueKind.Object
            || !record.TryGetProperty(property, out JsonElement value)
            || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return value.GetString();
    }

    private static SummerEbtCase MapCase(
        JsonElement record,
        Dictionary<string, FieldMapping> fields,
        Dictionary<string, EnumResolver> enumResolvers,
        Dictionary<string, KeywordRuleResolver> keywordResolvers)
    {
        var summerEbtCase = new SummerEbtCase();

        foreach ((string canonicalField, FieldMapping fieldMapping) in fields)
        {
            FieldTarget target = ResolveTarget(canonicalField);

            // The keyword-rules brick scans one-or-more free-text sources and always yields a value
            // (a keyword match or the default) — it has no single-source presence guard.
            if (fieldMapping.KeywordRules is not null)
            {
                target.SetEnum!(summerEbtCase, keywordResolvers[canonicalField].Resolve(record));
                continue;
            }

            if (!record.TryGetProperty(fieldMapping.From.Single, out JsonElement value)
                || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            ApplyField(summerEbtCase, canonicalField, target, fieldMapping, value, enumResolvers);
        }

        return summerEbtCase;
    }

    private static void ApplyField(
        SummerEbtCase target,
        string canonicalField,
        FieldTarget fieldTarget,
        FieldMapping fieldMapping,
        JsonElement value,
        Dictionary<string, EnumResolver> enumResolvers)
    {
        switch (fieldTarget.Kind)
        {
            case FieldKind.String:
                fieldTarget.SetString!(target, value.GetString() ?? string.Empty);
                break;

            case FieldKind.DateTime:
                fieldTarget.SetDateTime!(target, ParseDate(canonicalField, fieldMapping, value));
                break;

            case FieldKind.Enum:
                fieldTarget.SetEnum!(target, enumResolvers[canonicalField].Resolve(value.GetString()));
                break;

            default:
                throw new NotSupportedException(
                    $"Field kind '{fieldTarget.Kind}' is not supported by the response mapper.");
        }
    }

    private static DateTime ParseDate(string canonicalField, FieldMapping fieldMapping, JsonElement value)
    {
        string? raw = value.GetString();
        if (fieldMapping.Format is not { } format)
        {
            throw new InvalidOperationException(
                $"Date field '{canonicalField}' requires an exact 'format'.");
        }

        // Exact parse with the single configured format — no fallback, no transposition.
        return DateTime.ParseExact(raw!, format, CultureInfo.InvariantCulture, DateTimeStyles.None);
    }

    // Builds a validated token → canonical-value resolver for every enum-referencing field in this
    // mapping. Fails loud (invalid canonical value / ambiguous token) before any record is mapped.
    private static Dictionary<string, EnumResolver> BuildEnumResolvers(
        StateBackendConfiguration configuration,
        StateBackendResponseMapping mapping)
    {
        var resolvers = new Dictionary<string, EnumResolver>(StringComparer.Ordinal);

        foreach ((string canonicalField, FieldMapping fieldMapping) in mapping.Fields)
        {
            if (fieldMapping.Enum is not { } tableName)
            {
                continue;
            }

            FieldTarget target = ResolveTarget(canonicalField);
            if (target.EnumType is null)
            {
                throw new InvalidOperationException(
                    $"Field '{canonicalField}' references enum table '{tableName}' but is not an enum-typed target.");
            }

            StateBackendEnumTable table = ResolveTable(configuration, tableName);
            (Dictionary<string, object> tokenLookup, object? defaultValue) =
                BuildTokenLookup(tableName, table, target.EnumType);

            resolvers[canonicalField] = new EnumResolver(tableName, tokenLookup, defaultValue);
        }

        return resolvers;
    }

    // Builds a validated keyword-rule resolver for every keywordRules field in this mapping.
    // Fails loud (invalid canonical value / order not covering map keys) before any record is mapped.
    private static Dictionary<string, KeywordRuleResolver> BuildKeywordResolvers(
        StateBackendResponseMapping mapping)
    {
        var resolvers = new Dictionary<string, KeywordRuleResolver>(StringComparer.Ordinal);

        foreach ((string canonicalField, FieldMapping fieldMapping) in mapping.Fields)
        {
            if (fieldMapping.KeywordRules is not { } keywordRules)
            {
                continue;
            }

            (Type enumType, List<(object Value, List<string> Keywords)> ordered, object defaultValue) =
                ValidateKeywordRules(canonicalField, keywordRules);

            resolvers[canonicalField] = new KeywordRuleResolver(
                fieldMapping.From.All, ordered, defaultValue);
        }

        return resolvers;
    }

    // Validates a keywordRules brick against its enum-typed target, fail-loud:
    //   * the target field must be enum-typed;
    //   * every `order` entry, `map` key, and `default` must be a real member of that enum;
    //   * `order` must cover every `map` key (each keyword set is reachable).
    // Returns the target enum type, the ordered (value, keywords) pairs, and the parsed default.
    private static (Type EnumType, List<(object Value, List<string> Keywords)> Ordered, object Default)
        ValidateKeywordRules(string canonicalField, KeywordRules keywordRules)
    {
        FieldTarget target = ResolveTarget(canonicalField);
        if (target.EnumType is not { } enumType)
        {
            throw new InvalidOperationException(
                $"Field '{canonicalField}' declares keywordRules but is not an enum-typed target.");
        }

        // Order must cover every map key so no keyword set is silently unreachable.
        foreach (string mapKey in keywordRules.Map.Keys)
        {
            if (!keywordRules.Order.Contains(mapKey, StringComparer.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Field '{canonicalField}' keywordRules 'order' does not cover map key '{mapKey}'.");
            }
        }

        var ordered = new List<(object Value, List<string> Keywords)>();
        foreach (string ourValue in keywordRules.Order)
        {
            object parsed = ParseEnumMember($"keywordRules[{canonicalField}]", enumType, ourValue);
            List<string> keywords = keywordRules.Map.TryGetValue(ourValue, out List<string>? mapped)
                ? mapped
                : new List<string>();
            ordered.Add((parsed, keywords));
        }

        object defaultValue = ParseEnumMember($"keywordRules[{canonicalField}]", enumType, keywordRules.Default);

        return (enumType, ordered, defaultValue);
    }

    // Inverts a domain-centered table (our value → tokens) into a token → our-value lookup,
    // validating that each our-value is a real enum member and no token is ambiguous.
    private static (Dictionary<string, object> TokenLookup, object? Default) BuildTokenLookup(
        string tableName,
        StateBackendEnumTable table,
        Type enumType)
    {
        var tokenLookup = new Dictionary<string, object>(StringComparer.Ordinal);

        foreach ((string ourValue, List<string> tokens) in table.Map)
        {
            object parsed = ParseEnumMember(tableName, enumType, ourValue);

            foreach (string token in tokens)
            {
                if (tokenLookup.TryGetValue(token, out object? existing) && !existing.Equals(parsed))
                {
                    throw new InvalidOperationException(
                        $"Enum table '{tableName}' maps token '{token}' to more than one canonical value.");
                }

                tokenLookup[token] = parsed;
            }
        }

        object? defaultValue = table.Default is { } def
            ? ParseEnumMember(tableName, enumType, def)
            : null;

        return (tokenLookup, defaultValue);
    }

    private static object ParseEnumMember(string tableName, Type enumType, string memberName)
    {
        if (!Enum.TryParse(enumType, memberName, ignoreCase: false, out object? parsed) || parsed is null)
        {
            throw new InvalidOperationException(
                $"Enum table '{tableName}' references '{memberName}', which is not a member of {enumType.Name}.");
        }

        return parsed;
    }

    private static FieldTarget ResolveTarget(string canonicalField)
    {
        if (!FieldTargets.TryGetValue(canonicalField, out FieldTarget? target))
        {
            throw new NotSupportedException(
                $"Canonical field '{canonicalField}' is not supported by the response mapper.");
        }

        return target;
    }

    private static StateBackendEnumTable ResolveTable(StateBackendConfiguration configuration, string tableName)
    {
        if (configuration.Enums is null || !configuration.Enums.TryGetValue(tableName, out StateBackendEnumTable? table))
        {
            throw new InvalidOperationException(
                $"Response mapping references enum table '{tableName}', which is not defined under 'enums'.");
        }

        return table;
    }

    private static IEnumerable<StateBackendResponseMapping> ResponseMappings(StateBackendConfiguration configuration)
    {
        if (configuration.Operations.HouseholdLookup?.Response is { } lookup)
        {
            yield return lookup;
        }
    }

    /// <summary>
    /// Navigates a capped path: a leading <c>$</c>, dotted property segments, and <c>[index]</c>
    /// element access. Anything else is rejected — this is not a general JSONPath engine.
    /// </summary>
    private static JsonElement SelectPath(JsonElement root, string path)
    {
        JsonElement current = root;

        foreach (string segment in SplitPath(path))
        {
            int bracket = segment.IndexOf('[');
            string property = bracket >= 0 ? segment[..bracket] : segment;

            if (property.Length > 0)
            {
                if (current.ValueKind != JsonValueKind.Object
                    || !current.TryGetProperty(property, out current))
                {
                    return default;
                }
            }

            // Handle a trailing [index] on this segment, e.g. resultSets[0].
            while (bracket >= 0)
            {
                int close = segment.IndexOf(']', bracket);
                if (close < 0)
                {
                    throw new FormatException($"Malformed path segment '{segment}' in '{path}'.");
                }

                int index = int.Parse(segment[(bracket + 1)..close]);
                if (current.ValueKind != JsonValueKind.Array || index >= current.GetArrayLength())
                {
                    return default;
                }

                current = current[index];
                bracket = segment.IndexOf('[', close);
            }
        }

        return current;
    }

    private static IEnumerable<string> SplitPath(string path)
    {
        string trimmed = path.StartsWith("$.", StringComparison.Ordinal)
            ? path[2..]
            : path.StartsWith('$') ? path[1..] : path;

        return trimmed
            .Split('.', StringSplitOptions.RemoveEmptyEntries);
    }

    private enum FieldKind
    {
        String,
        DateTime,
        Enum,
    }

    /// <summary>
    /// A single canonical field target in the closed setter map: its coercion kind, the typed
    /// setter, and (for enums) the C# enum type its named table must resolve into.
    /// </summary>
    private sealed class FieldTarget
    {
        private FieldTarget(FieldKind kind)
        {
            Kind = kind;
        }

        public FieldKind Kind { get; }

        public Type? EnumType { get; private init; }

        public Action<SummerEbtCase, string>? SetString { get; private init; }

        public Action<SummerEbtCase, DateTime>? SetDateTime { get; private init; }

        public Action<SummerEbtCase, object>? SetEnum { get; private init; }

        public static FieldTarget String(Action<SummerEbtCase, string> setter) =>
            new(FieldKind.String) { SetString = setter };

        public static FieldTarget DateTime(Action<SummerEbtCase, DateTime> setter) =>
            new(FieldKind.DateTime) { SetDateTime = setter };

        public static FieldTarget Enum<TEnum>(Action<SummerEbtCase, TEnum> setter) where TEnum : struct, Enum =>
            new(FieldKind.Enum)
            {
                EnumType = typeof(TEnum),
                SetEnum = (target, value) => setter(target, (TEnum)value),
            };
    }

    /// <summary>
    /// A validated keyword-rule resolver: scans a record's source values for the ordered keyword
    /// sets, first-match-wins, returning the matched canonical enum value or the default. Matching
    /// is case-insensitive substring-contains — mirroring DC's <c>InferIssuanceType</c>.
    /// </summary>
    private sealed class KeywordRuleResolver
    {
        private readonly IReadOnlyList<string> _sources;
        private readonly List<(object Value, List<string> Keywords)> _ordered;
        private readonly object _default;

        public KeywordRuleResolver(
            IReadOnlyList<string> sources,
            List<(object Value, List<string> Keywords)> ordered,
            object defaultValue)
        {
            _sources = sources;
            _ordered = ordered;
            _default = defaultValue;
        }

        public object Resolve(JsonElement record)
        {
            // Collect the free-text source values once, uppercased for case-insensitive contains.
            var haystacks = new List<string>(_sources.Count);
            foreach (string source in _sources)
            {
                string? value = ReadString(record, source);
                if (!string.IsNullOrEmpty(value))
                {
                    haystacks.Add(value.ToUpperInvariant());
                }
            }

            // First canonical value whose ANY keyword is contained in ANY source wins.
            foreach ((object value, List<string> keywords) in _ordered)
            {
                foreach (string keyword in keywords)
                {
                    string needle = keyword.ToUpperInvariant();
                    foreach (string haystack in haystacks)
                    {
                        if (haystack.Contains(needle, StringComparison.Ordinal))
                        {
                            return value;
                        }
                    }
                }
            }

            return _default;
        }
    }

    /// <summary>A validated token → canonical-enum-value lookup with a fallback default.</summary>
    private sealed class EnumResolver
    {
        private readonly string _tableName;
        private readonly Dictionary<string, object> _tokenLookup;
        private readonly object? _default;

        public EnumResolver(string tableName, Dictionary<string, object> tokenLookup, object? defaultValue)
        {
            _tableName = tableName;
            _tokenLookup = tokenLookup;
            _default = defaultValue;
        }

        public object Resolve(string? token)
        {
            if (token is not null && _tokenLookup.TryGetValue(token, out object? value))
            {
                return value;
            }

            // Default applies ONLY to genuinely-unlisted tokens.
            return _default
                ?? throw new InvalidOperationException(
                    $"Enum table '{_tableName}' has no mapping for token '{token}' and no default.");
        }
    }
}
