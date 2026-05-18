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
}
