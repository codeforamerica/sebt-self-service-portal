namespace SEBT.Portal.Kernel;

/// <summary>
/// Service for retrieving feature flag states.
/// </summary>
public interface IFeatureFlagService
{
    /// <summary>
    /// Gets all configured feature flags as a dictionary.
    /// Only flags that are explicitly configured (enabled or disabled) are returned.
    /// Unknown flags are not included in the response.
    /// </summary>
    /// <returns>A dictionary of feature flag names to their enabled state (true = enabled, false = disabled).</returns>
    Dictionary<string, bool> GetFeatureFlags();
}

