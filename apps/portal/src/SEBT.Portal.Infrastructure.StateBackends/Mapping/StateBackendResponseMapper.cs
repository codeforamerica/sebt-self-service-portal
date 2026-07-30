using System.Globalization;
using System.Text.Json;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Maps a backend's raw JSON response into canonical domain types, driven by the operation's
/// <see cref="StateBackendResponseMapping"/> and named enum tables.
/// </summary>
internal static class StateBackendResponseMapper
{
    /// <summary>
    /// The closed set of canonical field targets, each with its coercion kind and (for enums) the
    /// target C# enum type. A new canonical field means a new entry here — never reflection.
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

    private enum FieldKind
    {
        String,
        DateTime,
        Enum,
    }

    /// <summary>
    /// Validates every enum table referenced by a response field mapping: each canonical value must
    /// be a real enum member, and no source token may appear under two canonical values. Invoked at
    /// load time by <see cref="Configuration.StateBackendConfigurationValidator"/>.
    /// </summary>
    internal static void ValidateEnumTables(StateBackendConfiguration configuration)
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
        JsonElement records = JsonPathSelector.Select(root, mapping.Root);
        var household = new HouseholdData();

        if (records.ValueKind != JsonValueKind.Array)
        {
            return household;
        }

        // Fail loud on bad enum tables / keyword rules before mapping any records.
        Dictionary<string, EnumResolver> enumResolvers = BuildEnumResolvers(configuration, mapping);
        Dictionary<string, KeywordRuleResolver> keywordResolvers = BuildKeywordResolvers(mapping);

        StateBackendDisaggregation? disaggregation = mapping.Disaggregation;

        // Application group keys, in first-seen order.
        var applicationKeys = new List<string>();
        var seenApplicationKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (JsonElement record in records.EnumerateArray())
        {
            // Map to canonical first — the inclusion predicate reads the canonical model, never raw
            // state fields.
            SummerEbtCase summerEbtCase = MapCase(record, mapping.Fields, enumResolvers, keywordResolvers);

            if (mapping.CaseId is { } caseIdComposition)
            {
                summerEbtCase.SummerEBTCaseID = ComposeCaseId(record, caseIdComposition);
            }

            if (disaggregation is null)
            {
                household.SummerEbtCases.Add(summerEbtCase);
                continue;
            }

            bool isApplicationBased = IsApplicationBased(record, disaggregation);

            // Every application-based record belongs to its grouped application, even when it isn't
            // included as a case (e.g. a pending application).
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
    /// The named case-inclusion predicate. For
    /// <see cref="CaseInclusionPredicate.WhenApprovedOrNotApplicationBased"/>, an unknown or unmapped
    /// <see cref="ApplicationStatus"/> is not Approved, so an application-based record with such a
    /// status is excluded (fail-closed).
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
        string? discriminator = JsonRead.AsString(record, disaggregation.DiscriminatorField);

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

        string? value = JsonRead.AsString(record, groupField);
        return string.IsNullOrEmpty(value) ? null : value;
    }

    // Packs the named routing fields off the record into an opaque caseId token. A routing field
    // absent from the record packs as empty; a later write that needs it fails loud.
    private static string ComposeCaseId(JsonElement record, CaseIdComposition composition)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string routingName, string sourceProperty) in composition.Fields)
        {
            fields[routingName] = JsonRead.AsString(record, sourceProperty) ?? string.Empty;
        }

        return OpaqueCaseId.Compose(fields);
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

            // keywordRules always yields a value (match or default), so it skips the presence guard.
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

        // Exact parse with the one configured format under InvariantCulture — no fallback.
        return DateTime.ParseExact(raw!, format, CultureInfo.InvariantCulture, DateTimeStyles.None);
    }

    // A validated token → canonical-value resolver per enum field, fail-loud before any record maps.
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

    // A validated keyword-rule resolver per keywordRules field, fail-loud before any record maps.
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

    // Validates a keywordRules brick against its enum-typed target (fail-loud), returning the enum
    // type, ordered (value, keywords) pairs, and parsed default.
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

    // Inverts a table (our value → tokens) into a token → our-value lookup, validating each our-value
    // is a real enum member and no token is ambiguous.
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
    /// A canonical field target: its coercion kind, typed setter, and (for enums) the target enum
    /// type.
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
    /// Scans a record's source values for the ordered keyword sets (first-match-wins,
    /// case-insensitive substring-contains), returning the matched canonical enum value or default.
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
            // Uppercased once for case-insensitive contains.
            var haystacks = new List<string>(_sources.Count);
            foreach (string source in _sources)
            {
                string? value = JsonRead.AsString(record, source);
                if (!string.IsNullOrEmpty(value))
                {
                    haystacks.Add(value.ToUpperInvariant());
                }
            }

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

            // Default applies only to genuinely-unlisted tokens.
            return _default
                ?? throw new InvalidOperationException(
                    $"Enum table '{_tableName}' has no mapping for token '{token}' and no default.");
        }
    }
}
