using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginCardReplacementRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementRequest;
using PluginCardReplacementResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementResult;

namespace SEBT.Portal.Api.Composition;

/// <summary>
/// Accepts card replacement requests without calling a state backend when
/// <see cref="FeatureFlags.UseMockHouseholdData"/> is on outside Production, since mock households
/// exist in no state system; otherwise sends them to the live plugin / configurable-backend path.
/// </summary>
internal sealed class FeatureGatedMockCardReplacementService(
    IConfiguration configuration,
    IHostEnvironment hostEnvironment,
    ILogger logger,
    ICardReplacementService live) : ICardReplacementService
{
    public Task<PluginCardReplacementResult> RequestCardReplacementAsync(
        PluginCardReplacementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!HouseholdMockGate.IsEnabled(configuration, hostEnvironment, logger))
        {
            return live.RequestCardReplacementAsync(request, cancellationToken);
        }

        logger.LogWarning(
            "Card replacement served from mock data. Feature flag {FeatureFlag} is enabled.",
            FeatureFlags.UseMockHouseholdData);
        return Task.FromResult(PluginCardReplacementResult.Success());
    }
}
