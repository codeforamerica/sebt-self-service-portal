namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Default feature flag settings used as fallback when no other source provides values.
/// </summary>
public class DefaultFeatureFlagSettings
{
    public static readonly string SectionName = "DefaultFeatureFlags";

    /// <summary>
    /// Dictionary of default feature flag names to their enabled state.
    /// These are used as fallback when state-specific or AWS AppConfig settings are not available.
    /// </summary>
    public Dictionary<string, bool> Flags { get; set; } = new();
}
