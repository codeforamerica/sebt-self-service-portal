using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Api.Models.Household;

/// <summary>
/// Request model for updating a household's mailing address.
/// </summary>
public record UpdateAddressRequest
{
    /// <summary>Street address line 1 (e.g., "123 Main St NW").</summary>
    [Required(ErrorMessage = "Street address is required.")]
    public required string StreetAddress1 { get; init; }

    /// <summary>Street address line 2 (e.g., apartment, suite). Optional.</summary>
    public string? StreetAddress2 { get; init; }

    /// <summary>City name.</summary>
    [Required(ErrorMessage = "City is required.")]
    public required string City { get; init; }

    /// <summary>State or territory name.</summary>
    [Required(ErrorMessage = "State is required.")]
    public required string State { get; init; }

    /// <summary>5- or 9-digit ZIP code (e.g., "20001" or "20001-1234").</summary>
    [Required(ErrorMessage = "Postal code is required.")]
    [RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "Postal code must be a valid 5- or 9-digit ZIP code.")]
    public required string PostalCode { get; init; }

    /// <summary>
    /// Persist the submitted address even when verification suggests an alternative (user chose entered address).
    /// </summary>
    public bool? AcceptEnteredAddress { get; init; }
}
