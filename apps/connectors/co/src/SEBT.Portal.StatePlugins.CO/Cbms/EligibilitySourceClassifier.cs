namespace SEBT.Portal.StatePlugins.CO.Cbms;

/// <summary>
/// Classifies CBMS EligSrc values into application-based vs streamlined certification.
/// </summary>
/// <remarks>
/// Known EligSrc values from CBMS:
/// - "CBMS", "PK" — application was submitted by a guardian
/// - "DIRC", "CDE" — streamlined certification (auto-eligible, no application submitted)
/// Any other value is unexpected; callers should treat as auto-eligible and log a warning.
/// </remarks>
internal static class EligibilitySourceClassifier
{
    private static readonly HashSet<string> ApplicationSources =
        new(StringComparer.OrdinalIgnoreCase) { "CBMS", "PK" };

    private static readonly HashSet<string> StreamlinedSources =
        new(StringComparer.OrdinalIgnoreCase) { "DIRC", "CDE" };

    public static bool IsApplicationBased(string? eligSrc) =>
        !string.IsNullOrEmpty(eligSrc) && ApplicationSources.Contains(eligSrc);

    public static bool IsStreamlinedCertification(string? eligSrc) =>
        !string.IsNullOrEmpty(eligSrc) && StreamlinedSources.Contains(eligSrc);
}
