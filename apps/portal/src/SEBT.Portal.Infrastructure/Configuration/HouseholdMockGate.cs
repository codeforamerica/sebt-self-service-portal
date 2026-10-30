using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Whether <see cref="FeatureFlags.UseMockHouseholdData"/> is in effect.
/// The flag is ignored when the host is Production.
/// </summary>
public static class HouseholdMockGate
{
    public static bool IsEnabled(
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var enabled = configuration.GetValue(
            $"FeatureManagement:{FeatureFlags.UseMockHouseholdData}",
            false);
        if (!enabled)
        {
            return false;
        }

        if (environment.IsProduction())
        {
            logger?.LogWarning(
                "Feature flag {FeatureFlag} is enabled in Production and will be ignored.",
                FeatureFlags.UseMockHouseholdData);
            return false;
        }

        return true;
    }
}
