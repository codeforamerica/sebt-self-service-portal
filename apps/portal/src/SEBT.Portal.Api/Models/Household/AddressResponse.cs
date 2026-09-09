namespace SEBT.Portal.Api.Models.Household;

/// <summary>
/// API response model for an address on file.
/// </summary>
public record AddressResponse
{
    /// <summary>
    /// The street address line 1.
    /// </summary>
    public string? StreetAddress1 { get; init; }

    /// <summary>
    /// The street address line 2 (apartment, suite, etc.).
    /// </summary>
    public string? StreetAddress2 { get; init; }

    /// <summary>
    /// The city.
    /// </summary>
    public string? City { get; init; }

    /// <summary>
    /// The state or province.
    /// </summary>
    public string? State { get; init; }

    /// <summary>
    /// The postal or ZIP code.
    /// </summary>
    public string? PostalCode { get; init; }
}
