using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Command to update the mailing address for an authenticated user's household.
/// </summary>
public class UpdateAddressCommand : ICommand
{
    /// <summary>
    /// The authenticated user's claims principal, used to resolve household identity.
    /// </summary>
    [Required]
    public required ClaimsPrincipal User { get; init; }

    [Required(ErrorMessage = "Street address is required.")]
    [RegularExpression(@"\S.*", ErrorMessage = "Street address cannot be whitespace only.")]
    public required string StreetAddress1 { get; init; }

    public string? StreetAddress2 { get; init; }

    [Required(ErrorMessage = "City is required.")]
    [RegularExpression(@"\S.*", ErrorMessage = "City cannot be whitespace only.")]
    public required string City { get; init; }

    [Required(ErrorMessage = "State is required.")]
    [RegularExpression(@"\S.*", ErrorMessage = "State cannot be whitespace only.")]
    public required string State { get; init; }

    [Required(ErrorMessage = "Postal code is required.")]
    [RegularExpression(@"^\d{5}(-\d{4})?$", ErrorMessage = "Postal code must be a valid 5- or 9-digit ZIP code.")]
    public required string PostalCode { get; init; }
}
