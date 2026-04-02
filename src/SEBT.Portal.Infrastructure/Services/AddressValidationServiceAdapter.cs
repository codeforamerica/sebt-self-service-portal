using System.Linq;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Maps <see cref="IAddressUpdateService"/> outcomes to the legacy <see cref="AddressValidationResult"/> shape.
/// </summary>
public sealed class AddressValidationServiceAdapter(IAddressUpdateService addressUpdateService) : IAddressValidationService
{
    public async Task<AddressValidationResult> ValidateAsync(Address address, CancellationToken cancellationToken = default)
    {
        var request = AddressUpdateOperationRequest.FromHouseholdAddress(address);
        var result = await addressUpdateService.ValidateAndNormalizeAsync(request, cancellationToken);

        return result switch
        {
            SuccessResult<AddressUpdateSuccess> ok =>
                AddressValidationResult.Valid(ok.Value.NormalizedAddress),
            ValidationFailedResult<AddressUpdateSuccess> vf =>
                AddressValidationResult.Invalid(string.Join(" ", vf.Errors.Select(e => e.Message))),
            // NOTE: DependencyFailed (timeout, non-2xx, network error) maps to Invalid here,
            // which obscures whether failure was an infrastructure issue or an actual bad address.
            // The legacy IAddressValidationService interface doesn't distinguish these cases.
            DependencyFailedResult<AddressUpdateSuccess> df =>
                AddressValidationResult.Invalid(df.Message),
            _ => AddressValidationResult.Invalid("Address verification failed.")
        };
    }
}
