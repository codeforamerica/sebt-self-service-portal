namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Computed permissions for self-service portal actions.
/// Evaluated server-side from <c>SelfServiceRulesSettings</c> against the user's
/// household data (issuance type, card status). The frontend reads these booleans
/// to show/hide CTAs without ever seeing the raw policy config.
/// </summary>
public record AllowedActions
{
    /// <summary>
    /// Whether the user can update their mailing address via the portal.
    /// </summary>
    public bool CanUpdateAddress { get; init; }

    /// <summary>
    /// Whether the user can request a replacement card via the portal.
    /// </summary>
    public bool CanRequestReplacementCard { get; init; }

    /// <summary>
    /// i18n key for the message shown when address update is denied.
    /// Null when <see cref="CanUpdateAddress"/> is true.
    /// </summary>
    public string? AddressUpdateDeniedMessageKey { get; init; }

    /// <summary>
    /// i18n key for the message shown when card replacement is denied.
    /// Null when <see cref="CanRequestReplacementCard"/> is true.
    /// </summary>
    public string? CardReplacementDeniedMessageKey { get; init; }
}
