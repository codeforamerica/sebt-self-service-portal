using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// When Smarty is disabled, performs trimming/ZIP formatting and enforces <see cref="AddressValidationPolicySettings"/>
/// (e.g. General Delivery) without calling an external API.
/// </summary>
public sealed class PassThroughAddressUpdateService(IOptions<AddressValidationPolicySettings> policySettings)
    : IAddressUpdateService
{
    public Task<Result<AddressUpdateSuccess>> ValidateAndNormalizeAsync(
        AddressUpdateOperationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var policy = policySettings.Value;
        var normalized = AddressNormalizationHelper.TrimToAddress(
            request.StreetAddress1,
            request.StreetAddress2,
            request.City,
            request.State,
            request.PostalCode);

        var isGeneralDelivery = GeneralDeliveryDetection.TextIndicatesGeneralDelivery(
            normalized.StreetAddress1,
            normalized.StreetAddress2);

        if (isGeneralDelivery && !policy.AllowGeneralDelivery)
        {
            return Task.FromResult(Result<AddressUpdateSuccess>.ValidationFailed(
                "streetAddress1",
                "General Delivery addresses are not accepted for this state."));
        }

        return Task.FromResult(
            Result<AddressUpdateSuccess>.Success(
                new AddressUpdateSuccess
                {
                    NormalizedAddress = normalized,
                    IsGeneralDelivery = isGeneralDelivery,
                    WasCorrected = false
                }));
    }
}
