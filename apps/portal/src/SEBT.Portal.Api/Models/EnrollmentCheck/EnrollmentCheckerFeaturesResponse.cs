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
