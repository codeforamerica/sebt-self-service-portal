using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Service implementation for retrieving feature flag states from configuration.
/// </summary>
public class FeatureFlagService : IFeatureFlagService
{
    private readonly FeatureFlagSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureFlagService"/> class.
    /// </summary>
    /// <param name="settings">The feature flag settings options.</param>
    public FeatureFlagService(IOptions<FeatureFlagSettings> settings)
    {
        _settings = settings.Value;
    }

    /// <summary>
    /// Gets all configured feature flags as a dictionary.
    /// Only flags that are explicitly configured (enabled or disabled) are returned.
    /// Unknown flags are not included in the response.
    /// </summary>
    /// <returns>A dictionary of feature flag names to their enabled state.</returns>
    public Dictionary<string, bool> GetFeatureFlags()
    {
        return new Dictionary<string, bool>(_settings.Flags ?? new Dictionary<string, bool>());
    }
}

