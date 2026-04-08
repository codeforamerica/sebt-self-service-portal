using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Evaluates self-service action permissions based on configuration rules.
/// </summary>
public interface ISelfServiceEvaluator
{
    /// <summary>
    /// Evaluates household-level permissions (address update).
    /// Uses permissive aggregation: if any case is eligible, the action is allowed.
    /// </summary>
    HouseholdAllowedActions EvaluateHousehold(IReadOnlyList<SummerEbtCase> cases);

    /// <summary>
    /// Evaluates per-case permissions (card replacement).
    /// Uses the case's issuance type and card status against configured rules.
    /// </summary>
    CaseAllowedActions EvaluateCase(SummerEbtCase summerEbtCase);
}
