using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Legacy façade over <see cref="IAddressUpdateService"/> for simple valid/invalid checks.
/// Prefer <see cref="IAddressUpdateService"/> for structured errors, identifiers, and metadata.
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
/// <param name="NormalizedAddress">USPS-normalized address when valid, if available.</param>
/// <param name="SuggestedAddress">An alternative address suggested by the validation service, if any.</param>
/// <param name="ErrorMessage">A user-facing error message if validation failed.</param>
public record AddressValidationResult(
    bool IsValid,
    Address? NormalizedAddress = null,
    Address? SuggestedAddress = null,
    string? ErrorMessage = null)
{
    public static AddressValidationResult Valid(Address? normalizedAddress = null) =>
        new(true, NormalizedAddress: normalizedAddress);

    public static AddressValidationResult Invalid(string errorMessage) => new(false, ErrorMessage: errorMessage);

    public static AddressValidationResult Suggestion(Address suggested) => new(false, SuggestedAddress: suggested);
}
