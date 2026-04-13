namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Computed permissions for household-level self-service actions.
/// Evaluated server-side from SelfServiceRulesSettings.
/// </summary>
public record HouseholdAllowedActions
{
    /// <summary>
    /// Whether the household can update their mailing address via the portal.
    /// </summary>
    public bool CanUpdateAddress { get; init; }

    /// <summary>
    /// i18n key for the message shown when address update is denied.
    /// Null when CanUpdateAddress is true.
    /// </summary>
    public string? AddressUpdateDeniedMessageKey { get; init; }

    /// <summary>
    /// Whether the household can request at least one replacement card via the portal.
    /// Permissive aggregation: true when any case qualifies for replacement.
    /// </summary>
    public bool CanRequestReplacementCard { get; init; }

    /// <summary>
    /// i18n key for the message shown when no case in the household qualifies for card replacement.
    /// Null when CanRequestReplacementCard is true.
    /// </summary>
    public string? CardReplacementDeniedMessageKey { get; init; }
}
