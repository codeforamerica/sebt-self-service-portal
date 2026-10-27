using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginAddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;
using PluginAddressUpdateResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateResult;

namespace SEBT.Portal.Api.Composition;

/// <summary>
/// Sends address updates to the in-memory mock when <see cref="FeatureFlags.UseMockHouseholdData"/>
/// is on outside Production, otherwise to the live plugin / configurable-backend path.
/// </summary>
internal sealed class FeatureGatedMockAddressUpdateService(
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    ILogger logger,
    Lazy<IAddressUpdateService> mock,
    IAddressUpdateService live) : IAddressUpdateService
{
    public Task<PluginAddressUpdateResult> UpdateAddressAsync(
        PluginAddressUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var service = HouseholdMockGate.IsEnabled(configuration, hostEnvironment, logger)
            ? mock.Value
            : live;
        return service.UpdateAddressAsync(request, cancellationToken);
    }
}
