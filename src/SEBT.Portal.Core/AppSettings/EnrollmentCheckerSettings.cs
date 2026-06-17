namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration served to the standalone enrollment checker app at runtime via the
/// public enrollment features endpoint. The checker is a statically-hosted frontend
/// with no server of its own, so any setting that must change without a redeploy
/// has to flow through this section.
///
/// Values can be overridden at runtime through the AWS AppConfig app-settings profile;
/// consumers must read them via <c>IOptionsMonitor&lt;T&gt;</c> so hot-reloaded values
/// take effect without an app restart.
/// </summary>
public class EnrollmentCheckerSettings
{
    public static readonly string SectionName = "EnrollmentChecker";

    /// <summary>
    /// Maintenance banner configuration for the enrollment checker.
    /// </summary>
    public MaintenanceBannerSettings MaintenanceBanner { get; set; } = new();
}

/// <summary>
/// Maintenance banner configuration for the enrollment checker. The on/off toggle is the
/// <see cref="FeatureFlags.EnableCheckerMaintenanceBanner"/> feature flag; this section
/// carries the banner copy itself.
/// </summary>
public class MaintenanceBannerSettings
{
    /// <summary>
    /// Per-language banner copy, keyed by lowercase ISO language code (e.g. "en", "es").
    /// The copy lives in configuration rather than the checker's locale bundles so it can
    /// be updated through AWS AppConfig without redeploying the statically-hosted checker.
    /// Language selection and fallback (active language, then English, otherwise hide)
    /// happen client-side.
    /// </summary>
    public Dictionary<string, string> Message { get; set; } = new();
}
