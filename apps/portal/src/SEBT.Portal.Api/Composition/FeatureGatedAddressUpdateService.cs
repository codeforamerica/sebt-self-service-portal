using Microsoft.FeatureManagement;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginAddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;
using PluginAddressUpdateResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateResult;

namespace SEBT.Portal.Api.Composition;

internal sealed class FeatureGatedAddressUpdateService(
    IFeatureManager featureManager,
    IAddressUpdateService plugin,
    IAddressUpdateBackend? adapter) : IAddressUpdateService
{
    public async Task<PluginAddressUpdateResult> UpdateAddressAsync(
        PluginAddressUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await ConfigurableStateBackendGate.UseAdapterAsync(featureManager, adapter))
        {
            return await plugin.UpdateAddressAsync(request, cancellationToken);
        }

        var coreRequest = new AddressUpdateRequest(
            request.HouseholdIdentifierValue,
            [],
            new AddressUpdateAddress
            {
                Line1 = request.Address.StreetAddress1,
                Line2 = request.Address.StreetAddress2,
                City = request.Address.City,
                State = request.Address.State,
                Zip = request.Address.PostalCode,
            });

        WriteResult result = await adapter!.UpdateAddressAsync(coreRequest, cancellationToken);
        return FeatureGatedWriteResultMapper.Map(
            result,
            PluginAddressUpdateResult.Success,
            PluginAddressUpdateResult.PolicyRejected,
            PluginAddressUpdateResult.BackendError);
    }
}
