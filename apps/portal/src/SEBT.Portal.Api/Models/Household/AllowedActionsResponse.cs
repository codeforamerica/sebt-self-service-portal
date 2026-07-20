namespace SEBT.Portal.Api.Models.Household;

/// <summary>
/// API response model for computed self-service action permissions.
/// </summary>
public record AllowedActionsResponse
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
    /// </summary>
    public string? AddressUpdateDeniedMessageKey { get; init; }

    /// <summary>
    /// i18n key for the message shown when card replacement is denied.
    /// </summary>
    public string? CardReplacementDeniedMessageKey { get; init; }
}
