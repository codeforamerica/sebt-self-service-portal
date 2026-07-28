using System.Text.Json;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Maps a state backend's raw JSON response into canonical domain types, driven by
/// <see cref="StateBackendResponseMapping"/>.
///
/// DC-568 spike scope (deliberately capped — see the prototype plan's STOP rule):
///   * Root selection supports ONLY simple dotted property access and <c>[index]</c> element
///     access (e.g. <c>$.resultSets[0]</c>). No JSONPath filters, wildcards, or recursion.
///   * Field mapping targets a CLOSED set of canonical <see cref="SummerEbtCase"/> string fields.
///   * Disaggregation supports classification (<see cref="DisaggregationRule.Presence"/> /
///     <see cref="DisaggregationRule.ValueInSet"/>), <see cref="CaseInclusionPredicate.All"/>
///     case inclusion, and grouping application-based records into applications by a single field.
///     <see cref="CaseInclusionPredicate.WhenApprovedOrNotApplicationBased"/> is NOT implemented —
///     it needs an approval-status field map not yet wired (design fork #2).
/// </summary>
internal static class StateBackendResponseMapper
{
    /// <summary>
    /// Maps the selected records into cases and, when disaggregation is configured, splits out
    /// grouped applications and links each application-based case to its application.
    /// </summary>
    public static HouseholdData MapHousehold(JsonElement root, StateBackendResponseMapping mapping)
    {
        JsonElement records = SelectPath(root, mapping.Root);
        var household = new HouseholdData();

        if (records.ValueKind != JsonValueKind.Array)
        {
            return household;
        }

        StateBackendDisaggregation? disaggregation = mapping.Disaggregation;

        // Group keys, in first-seen order, for the application-based records.
        var applicationKeys = new List<string>();
        var seenApplicationKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (JsonElement record in records.EnumerateArray())
        {
            SummerEbtCase summerEbtCase = MapCase(record, mapping.Fields);

            // Without disaggregation, records map 1:1 into a flat case list.
            if (disaggregation is null)
            {
                household.SummerEbtCases.Add(summerEbtCase);
                continue;
            }

            RequireSupportedCaseInclusion(disaggregation.CaseInclusion);

            if (IsApplicationBased(record, disaggregation)
                && GroupKey(record, disaggregation) is { } key)
            {
                summerEbtCase.ApplicationId = key;

                if (seenApplicationKeys.Add(key))
                {
                    applicationKeys.Add(key);
                }
            }

            // CaseInclusion.All: every record yields a case.
            household.SummerEbtCases.Add(summerEbtCase);
        }

        foreach (string key in applicationKeys)
        {
            household.Applications.Add(new Application { ApplicationNumber = key });
        }

        return household;
    }

    /// <summary>
    /// Named case-inclusion predicates are a closed vocabulary. Only <see cref="CaseInclusionPredicate.All"/>
    /// is implemented in this spike; the approval-aware predicate needs a status field map (fork #2).
    /// </summary>
    private static void RequireSupportedCaseInclusion(CaseInclusionPredicate caseInclusion)
    {
        if (caseInclusion != CaseInclusionPredicate.All)
        {
            throw new NotSupportedException(
                $"Case inclusion predicate '{caseInclusion}' is not implemented by the response mapper.");
        }
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

    private static SummerEbtCase MapCase(JsonElement record, Dictionary<string, string> fields)
    {
        var summerEbtCase = new SummerEbtCase();

        foreach ((string canonicalField, string sourceProperty) in fields)
        {
            if (!record.TryGetProperty(sourceProperty, out JsonElement value)
                || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            ApplyField(summerEbtCase, canonicalField, value.GetString() ?? string.Empty);
        }

        return summerEbtCase;
    }

    // Closed field-target map for the spike. A new canonical target requires adding a case here
    // rather than reflecting over property names — keeps the supported surface explicit.
    private static void ApplyField(SummerEbtCase target, string canonicalField, string value)
    {
        switch (canonicalField)
        {
            case "summerEBTCaseID":
                target.SummerEBTCaseID = value;
                break;
            case "childFirstName":
                target.ChildFirstName = value;
                break;
            case "childLastName":
                target.ChildLastName = value;
                break;
            case "applicationId":
                target.ApplicationId = value;
                break;
            default:
                throw new NotSupportedException(
                    $"Canonical field '{canonicalField}' is not supported by the response mapper.");
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
}
