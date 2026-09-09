using System.Text.Json.Serialization;

namespace SEBT.Portal.Api.Models.Household;

/// <summary>
/// API response for an address update request, including the validation outcome.
/// </summary>
public record AddressUpdateResponse
{
    /// <summary>
    /// The validation status: "valid", "invalid", or "suggestion".
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// The specific reason for the validation outcome (e.g., "blocked", "too_long", "abbreviated").
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }

    /// <summary>
    /// A user-facing error message when the address is invalid.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Message { get; init; }

    /// <summary>
    /// The normalized address that was persisted when Status is "valid".
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AddressResponse? NormalizedAddress { get; init; }

    /// <summary>
    /// A suggested alternative address (e.g., abbreviated street for DC 30-char limit,
    /// or a Smarty-corrected address). Present only when Status is "suggestion".
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public AddressResponse? SuggestedAddress { get; init; }
}
