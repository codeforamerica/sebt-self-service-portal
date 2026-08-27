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

    /// <summary>
    /// Income screening thresholds for the enrollment checker.
    /// </summary>
    public IncomeEligibilitySettings IncomeEligibility { get; set; } = new();
}

/// <summary>
/// Income screening thresholds for the checker's not-enrolled result. Toggled by the
/// <see cref="FeatureFlags.EnableCheckerIncomeEligibility"/> flag; this section carries
/// the figures. They track federal poverty guidelines, which are reissued annually.
/// </summary>
public class IncomeEligibilitySettings
{
    /// <summary>
    /// Annual gross income threshold for a household of one.
    /// </summary>
    public decimal BaseThreshold { get; set; }

    /// <summary>
    /// Added to the threshold for each household member beyond the first.
    /// </summary>
    public decimal PerMemberIncrement { get; set; }

    /// <summary>
    /// Largest household size the selector offers.
    /// </summary>
    public int MaxHouseholdSize { get; set; }
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
