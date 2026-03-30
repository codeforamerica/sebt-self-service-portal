using Microsoft.Extensions.Logging;
using Microsoft.FeatureManagement;
using SEBT.Portal.Kernel.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Service for querying feature flags.
/// Priority order is as follows, with latest being high priority:
/// 1. appsettings.json (defaults)
/// 2. State-specific JSON (appsettings.{State}.json)
/// 3. AWS AppConfig Agent (if configured — highest priority)
/// </summary>
public class FeatureFlagQueryService : IFeatureFlagQueryService
{
    private readonly IFeatureManager _featureManager;
    private readonly ILogger<FeatureFlagQueryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeatureFlagQueryService"/> class.
    /// </summary>
    /// <param name="featureManager">The feature manager from Microsoft.FeatureManagement.</param>
    /// <param name="logger">The logger.</param>
    public FeatureFlagQueryService(
        IFeatureManager featureManager,
        ILogger<FeatureFlagQueryService> logger)
    {
        _featureManager = featureManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets all configured feature flags as a dictionary.
    /// Flags are read from FeatureManager, which already has merged values from IConfiguration
    /// based on provider priority order configured at startup in Program.cs.
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
            await foreach (var featureName in _featureManager.GetFeatureNamesAsync().WithCancellation(cancellationToken))
            {
                if (IsValidFeatureFlagName(featureName))
                {
                    try
                    {
                        var isEnabled = await _featureManager.IsEnabledAsync(featureName);
                        flags[featureName] = isEnabled;
                        _logger.LogDebug("Feature flag {FeatureName}: {Value}", featureName, isEnabled);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to check feature flag {FeatureName}, skipping", featureName);
                        // Continue with other flags
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid feature flag name '{FeatureName}', skipping", featureName);
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
