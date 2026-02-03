namespace SEBT.Portal.Kernel.Services;

/// <summary>
/// Service for querying feature flags with priority order.
/// </summary>
public interface IFeatureFlagQueryService
{
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
    Task<Dictionary<string, bool>> GetFeatureFlagsAsync(CancellationToken cancellationToken = default);
}
