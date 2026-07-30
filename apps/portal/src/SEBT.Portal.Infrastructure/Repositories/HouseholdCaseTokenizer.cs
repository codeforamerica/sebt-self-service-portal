using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends;

namespace SEBT.Portal.Infrastructure.Repositories;

/// <summary>
/// Replaces each case's <c>SummerEBTCaseID</c> with an <see cref="OpaqueCaseId"/>
/// token so plugin-path reads emit case IDs in the same opaque namespace as the
/// JSON-adapter path. The token packs a fixed routing-field set: the raw case ID,
/// the application IDs, and the household identifier the lookup used — everything
/// a later write needs to route without re-deriving state-specific identifiers.
/// </summary>
/// <remarks>
/// Fields are packed in a fixed order and null/empty fields are omitted, so the
/// same source record always yields a byte-identical token — the frontend uses
/// the ID as a merge key between responses and as a lookup key across page
/// navigations. Guardian-facing display values (<c>EbtCaseNumber</c>,
/// <c>CaseDisplayNumber</c>, <c>EbtCardLastFour</c>) are never touched.
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
