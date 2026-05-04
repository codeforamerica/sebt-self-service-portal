using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Controls whether co-loaded Summer EBT cases are removed from the household payload for the
/// <see cref="CoLoadedCohort.MixedOrApplicantExcluded"/> cohort.
/// Classification (<see cref="HouseholdData.CoLoadedCohort"/>) always runs for analytics.
/// </summary>
/// <remarks>
/// Set <see cref="SuppressCoLoadedCasesForExcludedCohort"/> to <c>false</c> in environment-specific
/// config or AppConfig when ops need the full case list returned for that cohort (e.g., incident response).
/// </remarks>
public class CoLoadedCohortFilterSettings
{
    public static readonly string SectionName = "CoLoadedCohortFilter";

    /// <summary>
    /// When <c>true</c> (default), co-loaded cases are stripped from <see cref="HouseholdData.SummerEbtCases"/>
    /// for mixed-eligibility / applicant households and <see cref="HouseholdData.BenefitIssuanceType"/> is
    /// aligned with the filtered view.
    /// </summary>
    public bool SuppressCoLoadedCasesForExcludedCohort { get; set; } = true;
}
