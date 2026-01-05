namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration settings for feature flags.
/// Feature flags are configured per state in appsettings.{State}.json files.
/// </summary>
public class FeatureFlagSettings
{
    public static readonly string SectionName = "Features";

    /// <summary>
    /// Dictionary of feature flag names to their enabled state.
    /// Only flags that are explicitly configured will be included.
    /// </summary>
    public Dictionary<string, bool> Flags { get; set; } = new();
}

