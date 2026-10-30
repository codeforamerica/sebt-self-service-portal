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
    /// Uses the case's own issuance type, card status, and (for card replacement)
    /// optional days-before-expiration cutoff. Self-service actions are case-scoped,
    /// not household-scoped.
    /// </summary>
    AllowedActions Evaluate(SummerEbtCase summerEbtCase);

    /// <summary>
    /// Evaluates which self-service actions are permitted for the household as a whole.
    /// Uses permissive aggregation: if ANY case is eligible, the action is allowed.
    /// Used for top-level CTAs and for actions that operate on the household
    /// address rather than a specific case.
    /// </summary>
    AllowedActions EvaluateHousehold(IReadOnlyList<SummerEbtCase> summerEbtCases);

    /// <summary>
    /// Classifies a case's benefits as expiring or expired using the state-local date.
    /// Expiring uses the card-replacement days-before-expiration window, so it covers the
    /// same days that window hides replacement; expired applies whether or not it is set.
    /// </summary>
    /// <returns>The status, or null when neither applies or the case has no expiration date.</returns>
    BenefitExpirationStatus? GetBenefitExpirationStatus(SummerEbtCase summerEbtCase);

    /// <summary>
    /// The configured wait between card replacement requests for the same case.
    /// </summary>
    TimeSpan CardReplacementCooldownPeriod { get; }

    /// <summary>
    /// True while the case's last replacement request is more recent than now minus
    /// <see cref="CardReplacementCooldownPeriod"/>. Mirrors the portal-DB check on the
    /// request path so the UI and the API agree; a case with no request is never in cooldown.
    /// </summary>
    bool IsInCardReplacementCooldown(SummerEbtCase summerEbtCase);
}
