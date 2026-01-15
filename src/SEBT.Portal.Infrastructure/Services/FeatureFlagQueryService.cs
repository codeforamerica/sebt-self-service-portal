using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Service for querying feature flags with priority order (later sources override earlier ones):
/// 1. Default feature flags (lowest priority - base)
/// 2. AWS AppConfig (if configured)
/// 3. State-specific JSON files (appsettings.{State}.json) - highest priority
/// FeatureManager provides any additional flags not defined in the above sources
/// </summary>
public class FeatureFlagQueryService : IFeatureFlagQueryService
{
    private readonly IFeatureManager _featureManager;
    private readonly DefaultFeatureFlagSettings _defaultFlags;
    private readonly FeatureManagementSettings _featureManagementFlags;
    private readonly AppConfigFeatureFlagSettings _appConfigFlags;
    private readonly ILogger<FeatureFlagQueryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureFlagQueryService"/> class.
    /// </summary>
    /// <param name="featureManager">The feature manager from Microsoft.FeatureManagement.</param>
    /// <param name="defaultFlags">The default feature flag settings.</param>
    /// <param name="featureManagementFlags">The feature management settings from FeatureManagement section.</param>
    /// <param name="appConfigFlags">The AWS AppConfig feature flag settings.</param>
    /// <param name="logger">The logger.</param>
    public FeatureFlagQueryService(
        IFeatureManager featureManager,
        IOptions<DefaultFeatureFlagSettings> defaultFlags,
        IOptions<FeatureManagementSettings> featureManagementFlags,
        IOptions<AppConfigFeatureFlagSettings> appConfigFlags,
        ILogger<FeatureFlagQueryService> logger)
    {
        _featureManager = featureManager;
        _defaultFlags = defaultFlags.Value;
        _featureManagementFlags = featureManagementFlags.Value;
        _appConfigFlags = appConfigFlags.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets all configured feature flags as a dictionary.
    /// Checks sources in priority order (later sources override earlier ones):
    /// 1. Default feature flags (lowest priority - base)
    /// 2. AWS AppConfig (if configured)
    /// 3. State-specific JSON files (appsettings.{State}.json) - highest priority
    /// FeatureManager provides any additional flags not defined in the above sources
    /// Only flags that are explicitly configured (enabled or disabled) are returned.
    /// Unknown flags are not included in the response.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A dictionary of feature flag names to their enabled state.</returns>
    public async Task<Dictionary<string, bool>> GetFeatureFlagsAsync(CancellationToken cancellationToken = default)
    {
        var flags = new Dictionary<string, bool>();

        try
        {
            // Priority 1: Start with default feature flags as base (lowest priority)
            if (_defaultFlags?.Flags != null)
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
            if (_appConfigFlags.Enabled && _appConfigFlags.Features != null)
            {
                foreach (var (key, value) in _appConfigFlags.Features)
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
            }

            // Priority 3: Override with state-specific JSON configuration (highest priority - applied last)
            // State JSON has the highest priority and will override all other sources
            if (_featureManagementFlags.Flags != null)
            {
                foreach (var (key, value) in _featureManagementFlags.Flags)
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
            }

            // FeatureManager provides any additional flags not defined in the above sources
            // These are flags that might be configured directly in FeatureManagement but not in our priority sources
            await foreach (var featureName in _featureManager.GetFeatureNamesAsync().WithCancellation(cancellationToken))
            {
                if (!flags.ContainsKey(featureName))
                {
                    if (IsValidFeatureFlagName(featureName))
                    {
                        try
                        {
                            var isEnabled = await _featureManager.IsEnabledAsync(featureName);
                            flags[featureName] = isEnabled;
                            _logger.LogDebug("Feature flag {FeatureName} set from FeatureManager: {Value}", featureName, isEnabled);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to check feature flag {FeatureName} from FeatureManager, skipping", featureName);
                            // Continue with other flags
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Invalid feature flag name '{FeatureName}' from FeatureManager, skipping", featureName);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Feature flag query was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve feature flags");
            throw;
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
