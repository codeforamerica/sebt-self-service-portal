namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Computed permissions for per-case self-service actions.
/// Evaluated server-side from SelfServiceRulesSettings against the case's
/// issuance type and card status.
/// </summary>
public record CaseAllowedActions
{
    /// <summary>
    /// Whether a replacement card can be requested for this case.
    /// </summary>
    public bool CanRequestReplacementCard { get; init; }

    /// <summary>
    /// i18n key for the message shown when card replacement is denied.
    /// Null when CanRequestReplacementCard is true.
    /// </summary>
    public string? CardReplacementDeniedMessageKey { get; init; }
}
