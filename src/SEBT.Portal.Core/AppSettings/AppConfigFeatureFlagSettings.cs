namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// AWS AppConfig feature flag settings.
/// </summary>
public class AppConfigFeatureFlagSettings
{
    public static readonly string SectionName = "FeatureManagement:AppConfig";

    /// <summary>
    /// Indicates whether AWS AppConfig is enabled for feature flags.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Dictionary of feature flag names to their enabled state from AppConfig.
    /// These are read from FeatureManagement:AppConfig:Features section.
    /// </summary>
    public Dictionary<string, bool> Features { get; set; } = new();
}
