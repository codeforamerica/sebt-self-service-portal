using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Stub address validation service that always returns valid.
/// Replace with a real Smarty integration when DC-160 is implemented.
/// </summary>
public class AlwaysValidAddressValidator : IAddressValidationService
{
    public Task<AddressValidationResult> ValidateAsync(Address address, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(AddressValidationResult.Valid());
    }
}
