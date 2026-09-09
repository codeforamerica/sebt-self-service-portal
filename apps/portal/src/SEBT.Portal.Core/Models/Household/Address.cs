namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents an address on file for a household.
/// </summary>
public class Address
{
    /// <summary>
    /// The street address line 1.
    /// </summary>
    public string? StreetAddress1 { get; set; }

    /// <summary>
    /// The street address line 2 (apartment, suite, etc.).
    /// </summary>
    public string? StreetAddress2 { get; set; }

    /// <summary>
    /// The city.
    /// </summary>
    public string? City { get; set; }

    /// <summary>
    /// The state or province.
    /// </summary>
    public string? State { get; set; }

    /// <summary>
    /// The postal or ZIP code.
    /// </summary>
    public string? PostalCode { get; set; }
}
