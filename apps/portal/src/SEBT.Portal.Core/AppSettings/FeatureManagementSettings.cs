namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Feature management settings from the FeatureManagement configuration section.
/// This includes state-specific feature flags from appsettings.{State}.json files.
/// </summary>
public class FeatureManagementSettings
{
    public static readonly string SectionName = "FeatureManagement";

    /// <summary>
    /// Dictionary of feature flag names to their enabled state.
    /// These are read from the FeatureManagement section, excluding the AppConfig subsection.
    /// </summary>
    public Dictionary<string, bool> Flags { get; set; } = new();
}
