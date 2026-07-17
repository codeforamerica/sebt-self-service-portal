namespace SEBT.Portal.Api.Models.EnrollmentCheck;

/// <summary>
/// Runtime feature state for the standalone enrollment checker app.
/// Served unauthenticated so the statically-hosted checker can poll it at runtime.
/// </summary>
public class EnrollmentCheckerFeaturesResponse
{
    /// <summary>
    /// Maintenance banner state.
    /// </summary>
    public MaintenanceBannerFeature MaintenanceBanner { get; init; } = new();

    /// <summary>
    /// Sitewide outage page state. Driven by OutageSchedule windows targeting the
    /// enrollment checker, with the checker_outage_page_enabled flag as the manual
    /// fallback when no such windows are configured.
    /// </summary>
    public OutagePageFeature OutagePage { get; init; } = new();
}

/// <summary>
/// Outage page state for the enrollment checker. When enabled, the checker replaces all
/// routes with a full-page outage notice.
/// </summary>
public class OutagePageFeature
{
    /// <summary>
    /// Whether the outage page should be shown.
    /// </summary>
    public bool Enabled { get; init; }
}

/// <summary>
/// Maintenance banner state for the enrollment checker.
/// </summary>
public class MaintenanceBannerFeature
{
    /// <summary>
    /// Whether the maintenance banner should be shown.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Per-language banner copy, keyed by lowercase ISO language code (e.g. "en", "es").
    /// The checker picks the active language client-side.
    /// </summary>
    public Dictionary<string, string> Message { get; init; } = new();
}
