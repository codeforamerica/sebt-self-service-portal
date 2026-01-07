using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Service implementation for retrieving feature flag states from configuration.
/// Merges plugin defaults with configuration file settings (configuration takes precedence).
/// </summary>
public class FeatureFlagService : IFeatureFlagService
{
    private readonly FeatureFlagSettings _settings;
    private readonly IStatePluginRegistry _pluginRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureFlagService"/> class.
    /// </summary>
    /// <param name="settings">The feature flag settings options.</param>
    /// <param name="pluginRegistry">The state plugin registry.</param>
    public FeatureFlagService(IOptions<FeatureFlagSettings> settings, IStatePluginRegistry pluginRegistry)
    {
        _settings = settings.Value;
        _pluginRegistry = pluginRegistry;
    }

    /// <summary>
    /// Gets all configured feature flags as a dictionary.
    /// Merges plugin defaults with configuration file settings (configuration takes precedence).
    /// Only flags that are explicitly configured (enabled or disabled) are returned.
    /// Unknown flags are not included in the response.
    /// </summary>
    /// <returns>A dictionary of feature flag names to their enabled state.</returns>
    public Dictionary<string, bool> GetFeatureFlags()
    {
        var flags = new Dictionary<string, bool>();

        // Start with plugin defaults
        var activePlugin = _pluginRegistry.GetActivePlugin();
        if (activePlugin != null)
        {
            var pluginDefaults = activePlugin.GetDefaultFeatureFlags();
            foreach (var (key, value) in pluginDefaults)
            {
                flags[key] = value;
            }
        }

        // Override with configuration file settings (config takes precedence)
        var configFlags = _settings.Flags ?? new Dictionary<string, bool>();
        foreach (var (key, value) in configFlags)
        {
            flags[key] = value;
        }

        return flags;
    }
}

