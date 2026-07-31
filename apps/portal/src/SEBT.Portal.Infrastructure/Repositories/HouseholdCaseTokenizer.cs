using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// Replaces each case's <c>SummerEBTCaseID</c> with an <see cref="OpaqueCaseId"/> token so
/// plugin-path reads emit case IDs in the same opaque namespace as the JSON-adapter path. The
/// token packs everything a later write needs to route without re-deriving state identifiers.
/// </summary>
/// <remarks>
/// Fixed packing order and omitted empty fields keep tokens byte-identical per record — the
/// frontend uses the ID as a merge and lookup key. Guardian-facing display values are never touched.
/// </remarks>
internal static class HouseholdCaseTokenizer
{
    public static void ReplaceCaseIdsWithTokens(HouseholdData household, string householdIdentifier)
    {
        ArgumentNullException.ThrowIfNull(household);
        ArgumentException.ThrowIfNullOrWhiteSpace(householdIdentifier);

        foreach (var summerEbtCase in household.SummerEbtCases)
        {
            if (summerEbtCase.SummerEBTCaseID == null)
            {
                continue;
            }

            // Fixed insertion order — determinism depends on it.
            var routingFields = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["caseId"] = summerEbtCase.SummerEBTCaseID,
            };
            if (!string.IsNullOrEmpty(summerEbtCase.ApplicationId))
            {
                routingFields["applicationId"] = summerEbtCase.ApplicationId;
            }
            if (!string.IsNullOrEmpty(summerEbtCase.ApplicationStudentId))
            {
                routingFields["applicationStudentId"] = summerEbtCase.ApplicationStudentId;
            }
            routingFields["householdIdentifier"] = householdIdentifier;

            summerEbtCase.SummerEBTCaseID = OpaqueCaseId.Compose(routingFields);
        }
    }
}
