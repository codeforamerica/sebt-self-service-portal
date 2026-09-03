namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Optional gate before the Socure DocV path. When enabled (DC), users without a
/// portal household record or with no cases and no applications are blocked from
/// Socure — same rule as the dashboard empty state.
/// </summary>
public class IdProofingEligibilitySettings : IHaveConfigSectionName
{
    public static string SectionName => "IdProofingEligibility";

    /// <summary>
    /// When true, ID proofing submissions that would call Socure are rejected if
    /// the household is missing or has no summer EBT cases and no applications.
    /// </summary>
    public bool RequireQualifyingHouseholdForSocure { get; set; }
}
