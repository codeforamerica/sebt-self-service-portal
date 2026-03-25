using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Validates a mailing address against an external service (e.g., Smarty).
/// Implementations may autocomplete, suggest alternatives, or reject invalid addresses.
/// </summary>
public interface IAddressValidationService
{
    /// <summary>
    /// Validates the given address and returns a result indicating whether the address is valid,
    /// invalid, or has a suggested alternative.
    /// </summary>
    Task<AddressValidationResult> ValidateAsync(Address address, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of an address validation call.
/// </summary>
/// <param name="IsValid">Whether the address passed validation.</param>
/// <param name="SuggestedAddress">An alternative address suggested by the validation service, if any.</param>
/// <param name="ErrorMessage">A user-facing error message if validation failed.</param>
public record AddressValidationResult(
    bool IsValid,
    Address? SuggestedAddress = null,
    string? ErrorMessage = null)
{
    public static AddressValidationResult Valid() => new(true);

    public static AddressValidationResult Invalid(string errorMessage) => new(false, ErrorMessage: errorMessage);

    public static AddressValidationResult Suggestion(Address suggested) => new(false, SuggestedAddress: suggested);
}
