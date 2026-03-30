using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Evaluates self-service action permissions based on configuration rules
/// and the user's household data.
/// </summary>
public interface ISelfServiceEvaluator
{
    /// <summary>
    /// Evaluates which self-service actions are permitted for the given household.
    /// Uses permissive aggregation: if ANY application is eligible, the action is allowed.
    /// </summary>
    /// <param name="householdIssuanceType">
    /// The household-level benefit issuance type. Used as a fallback when no applications exist.
    /// </param>
    /// <param name="applications">
    /// The household's applications, each with its own issuance type and card status.
    /// </param>
    AllowedActions Evaluate(BenefitIssuanceType householdIssuanceType, IReadOnlyList<Application> applications);
}
