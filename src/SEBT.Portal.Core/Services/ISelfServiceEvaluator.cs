using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Evaluates self-service action permissions based on configuration rules
/// and the user's household data.
/// </summary>
public interface ISelfServiceEvaluator
{
    /// <summary>
    /// Evaluates which self-service actions are permitted for a single case.
    /// Uses the case's own <see cref="SummerEbtCase.IssuanceType"/> and
    /// <see cref="SummerEbtCase.CardStatus"/>. Per James's 4.3.26 guidance,
    /// self-service actions are case-scoped, not household-scoped.
    /// </summary>
    AllowedActions Evaluate(SummerEbtCase summerEbtCase);

    /// <summary>
    /// Evaluates which self-service actions are permitted for the household as a whole.
    /// Uses permissive aggregation: if ANY case is eligible, the action is allowed.
    /// Used for top-level CTAs and for actions that operate on the household
    /// address rather than a specific case.
    /// </summary>
    AllowedActions EvaluateHousehold(IReadOnlyList<SummerEbtCase> summerEbtCases);
}
