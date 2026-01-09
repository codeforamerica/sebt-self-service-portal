using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Service implementation for retrieving feature flag states with priority order (later sources override earlier ones):
/// 1. Default feature flags (lowest priority - base)
/// 2. AWS AppConfig (if configured)
/// 3. State-specific JSON files (appsettings.{State}.json) - highest priority
/// </summary>
public class FeatureFlagService : IFeatureFlagService
{
    private readonly IFeatureManager _featureManager;
    private readonly IConfiguration _configuration;
    private readonly DefaultFeatureFlagSettings _defaultFlags;
    private readonly ILogger<FeatureFlagService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureFlagService"/> class.
    /// </summary>
    /// <param name="featureManager">The feature manager from Microsoft.FeatureManagement.</param>
    /// <param name="configuration">The application configuration.</param>
    /// <param name="defaultFlags">The default feature flag settings.</param>
    /// <param name="logger">The logger.</param>
    public FeatureFlagService(
        IFeatureManager featureManager,
        IConfiguration configuration,
        IOptions<DefaultFeatureFlagSettings> defaultFlags,
        ILogger<FeatureFlagService> logger)
    {
        _featureManager = featureManager;
        _configuration = configuration;
        _defaultFlags = defaultFlags.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets all configured feature flags as a dictionary.
    /// Checks sources in priority order (later sources override earlier ones):
    /// 1. Default feature flags (lowest priority - base)
    /// 2. AWS AppConfig (if configured)
    /// 3. State-specific JSON files (appsettings.{State}.json) - highest priority
    /// Only flags that are explicitly configured (enabled or disabled) are returned.
    /// Unknown flags are not included in the response.
    /// </summary>
    /// <returns>A dictionary of feature flag names to their enabled state.</returns>
    public async Task<Dictionary<string, bool>> GetFeatureFlagsAsync()
    {
        var flags = new Dictionary<string, bool>();

        // Priority 1: Start with default feature flags as base (lowest priority)
        if (_defaultFlags.Flags != null)
        {
            foreach (var (key, value) in _defaultFlags.Flags)
            {
                if (IsValidFeatureFlagName(key))
                {
                    flags[key] = value;
                }
                else
                {
                    _logger.LogWarning("Invalid feature flag name '{FeatureName}' in default flags, skipping", key);
                }
            }
        }

        // Priority 2: Override with AWS AppConfig if configured
        var appConfigFlags = GetAppConfigFeatureFlags();
        foreach (var (key, value) in appConfigFlags)
        {
            if (IsValidFeatureFlagName(key))
            {
                flags[key] = value;
                _logger.LogDebug("Feature flag {FeatureName} set from AWS AppConfig: {Value}", key, value);
            }
            else
            {
                _logger.LogWarning("Invalid feature flag name '{FeatureName}' in AppConfig, skipping", key);
            }
        }

        // Priority 3: Override with state-specific JSON configuration (highest priority - applied last)
        // State JSON has the highest priority and will override all other sources
        var stateJsonFlags = GetStateJsonFeatureFlags();
        foreach (var (key, value) in stateJsonFlags)
        {
            if (IsValidFeatureFlagName(key))
            {
                flags[key] = value;
                _logger.LogDebug("Feature flag {FeatureName} set from state JSON: {Value}", key, value);
            }
            else
            {
                _logger.LogWarning("Invalid feature flag name '{FeatureName}' in state JSON, skipping", key);
            }
        }

        // Also include any flags from FeatureManager that aren't in our dictionary
        // These are flags that might be configured directly in FeatureManagement but not in our priority sources
        await foreach (var featureName in _featureManager.GetFeatureNamesAsync())
        {
            if (!flags.ContainsKey(featureName))
            {
                if (IsValidFeatureFlagName(featureName))
                {
                    var isEnabled = await _featureManager.IsEnabledAsync(featureName);
                    flags[featureName] = isEnabled;
                    _logger.LogDebug("Feature flag {FeatureName} set from FeatureManager: {Value}", featureName, isEnabled);
                }
                else
                {
                    _logger.LogWarning("Invalid feature flag name '{FeatureName}' from FeatureManager, skipping", featureName);
                }
            }
        }

        return flags;
    }

    /// <summary>
    /// Gets feature flags from state-specific JSON files (appsettings.{State}.json).
    /// Skips the AppConfig section to avoid reading configuration metadata.
    /// </summary>
    private Dictionary<string, bool> GetStateJsonFeatureFlags()
    {
        var flags = new Dictionary<string, bool>();
        var featureManagementSection = _configuration.GetSection("FeatureManagement");

        if (featureManagementSection.Exists())
        {
            foreach (var child in featureManagementSection.GetChildren())
            {
                // Skip AppConfig configuration section itself
                if (child.Key.Equals("AppConfig", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (child.Value != null && bool.TryParse(child.Value, out var boolValue))
                {
                    flags[child.Key] = boolValue;
                }
            }
        }

        return flags;
    }

    /// <summary>
    /// Gets feature flags from AWS AppConfig if configured.
    /// Checks for AWS AppConfig configuration in the FeatureManagement section.
    /// </summary>
    private Dictionary<string, bool> GetAppConfigFeatureFlags()
    {
        var flags = new Dictionary<string, bool>();

        // Check if AWS AppConfig is configured
        var appConfigSection = _configuration.GetSection("FeatureManagement:AppConfig");

        if (appConfigSection.Exists())
        {
            // Check if AppConfig is enabled
            try
            {
                var enabled = appConfigSection.GetValue<bool>("Enabled", false);
                if (!enabled)
                {
                    return flags;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read AppConfig Enabled setting, assuming disabled");
                return flags;
            }

            // Try to read feature flags from AppConfig configuration source
            // This assumes AWS AppConfig Agent is running locally or direct API calls are configured
            var appConfigFeatureSection = _configuration.GetSection("FeatureManagement:AppConfig:Features");

            if (appConfigFeatureSection.Exists())
            {
                foreach (var child in appConfigFeatureSection.GetChildren())
                {
                    if (child.Value != null && bool.TryParse(child.Value, out var boolValue))
                    {
                        flags[child.Key] = boolValue;
                    }
                    else
                    {
                        try
                        {
                            var enabledValue = child.GetValue<bool?>("Enabled");
                            if (enabledValue.HasValue)
                            {
                                flags[child.Key] = enabledValue.Value;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(ex, "Failed to parse AppConfig feature flag {FeatureName}", child.Key);
                        }
                    }
                }
            }
            else
            {
                // If AppConfig is enabled but no features section exists,
                // try to read from the main FeatureManagement section which might be populated by AppConfig Agent
                var featureManagementSection = _configuration.GetSection("FeatureManagement");
                foreach (var child in featureManagementSection.GetChildren())
                {
                    // Skip AppConfig configuration section itself
                    if (child.Key.Equals("AppConfig", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (child.Value != null && bool.TryParse(child.Value, out var boolValue))
                    {
                        flags[child.Key] = boolValue;
                    }
                }
            }
        }

        return flags;
    }

    /// <summary>
    /// Validates that a feature flag name follows naming conventions.
    /// Feature flag names should contain only alphanumeric characters and underscores.
    /// </summary>
    /// <param name="name">The feature flag name to validate.</param>
    /// <returns>True if the name is valid, false otherwise.</returns>
    private static bool IsValidFeatureFlagName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // Allow alphanumeric characters and underscores only to follow AppConfig FF format
        // See: https://docs.aws.amazon.com/appconfig/latest/userguide/appconfig-agent-how-to-use-local-development-samples.html
        return name.All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}
