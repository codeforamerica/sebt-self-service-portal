namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Constants for feature flag names used across the portal.
/// Names must contain only alphanumeric characters and underscores to satisfy both
/// the FeatureFlagQueryService validator and AWS AppConfig naming requirements.
/// </summary>
public static class FeatureFlags
{
    /// <summary>
    /// When enabled, the portal drops enrollment check candidates (Match/PossibleMatch)
    /// where neither the date of birth nor the full name (first + last) exactly matches
    /// what was submitted. Defaults to true for CO via appsettings.co.json.
    /// </summary>
    public const string EnrollmentCheckRequiresAtLeastOneExactMatchedField =
        "enrollment_check_requires_at_least_one_exact_matched_field";

    /// <summary>
    /// When enabled (CO), the dashboard may request household data without CBMS FIS card
    /// details first, then load card fields in a follow-up request. Defaults to false.
    /// </summary>
    public const string DeferEbtCardDataLoading = "defer_ebt_card_data_loading";

    /// <summary>
    /// When enabled, the diagnostic test-error endpoints under /api/test-error are active.
    /// Disabled by default; enable in Development or staging via appsettings.Development.json
    /// or AWS AppConfig. Never enable in production.
    /// </summary>
    public const string TestErrorEndpointsEnabled = "test_error_endpoints_enabled";

    /// <summary>
    /// When enabled, the portal shows a sitewide outage page instead of normal routes.
    /// State partners can toggle via appsettings.{State}.json or AWS AppConfig.
    /// </summary>
    public const string OutagePageEnabled = "outage_page_enabled";

    /// <summary>
    /// When enabled, the standalone enrollment checker app shows a maintenance banner.
    /// Banner copy comes from the EnrollmentChecker:MaintenanceBanner:Message per-language
    /// map (see <see cref="MaintenanceBannerSettings"/>); the checker selects the active
    /// language's copy client-side. Toggle at runtime via AWS AppConfig; the checker polls
    /// the enrollment features endpoint, so no checker redeploy is required.
    /// </summary>
    public const string EnableCheckerMaintenanceBanner = "enable_checker_maintenance_banner";

    /// <summary>
    /// When enabled, the standalone enrollment checker app shows a sitewide outage page
    /// instead of normal routes. Manual counterpart to OutageSchedule windows targeting
    /// EnrollmentChecker: with no such windows configured, this flag is the authority
    /// (for unscheduled emergencies); when windows targeting the checker exist, the
    /// schedule wins and this flag is ignored. Toggle at runtime via AWS AppConfig; the
    /// checker polls the enrollment features endpoint, so no checker redeploy is required.
    /// </summary>
    public const string CheckerOutagePageEnabled = "checker_outage_page_enabled";
}
