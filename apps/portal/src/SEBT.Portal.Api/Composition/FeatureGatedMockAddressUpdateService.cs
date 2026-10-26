using Microsoft.FeatureManagement;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginAddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;
using PluginAddressUpdateResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateResult;

namespace SEBT.Portal.Api.Composition;

/// <summary>
/// Sends address updates to the in-memory mock when <see cref="FeatureFlags.UseMockHouseholdData"/>
/// is on, otherwise to the live plugin / configurable-backend path.
/// </summary>
internal sealed class FeatureGatedMockAddressUpdateService(
    IFeatureManager featureManager,
    IAddressUpdateService mock,
    IAddressUpdateService live) : IAddressUpdateService
{
    public async Task<PluginAddressUpdateResult> UpdateAddressAsync(
        PluginAddressUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var service = await featureManager.IsEnabledAsync(FeatureFlags.UseMockHouseholdData)
            ? mock
            : live;
        return await service.UpdateAddressAsync(request, cancellationToken);
    }
}
