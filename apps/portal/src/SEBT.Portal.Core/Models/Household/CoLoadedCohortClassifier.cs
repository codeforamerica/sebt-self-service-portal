namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Derives <see cref="CoLoadedCohort"/> from pre-filter household case and application state.
/// Shared by household reads and ID proofing off-boarding decisions.
/// </summary>
public static class CoLoadedCohortClassifier
{
    /// <summary>
    /// Classifies the household based on its case list and applications.
    /// See <see cref="CoLoadedCohort"/> for the rule.
    /// </summary>
    public static CoLoadedCohort Classify(HouseholdData? household)
    {
        if (household == null)
        {
            return CoLoadedCohort.NonCoLoaded;
        }

        var hasCoLoaded = household.SummerEbtCases.Any(c => c.IsCoLoaded);
        if (!hasCoLoaded)
        {
            return CoLoadedCohort.NonCoLoaded;
        }

        var hasNonCoLoaded = household.SummerEbtCases.Any(c => !c.IsCoLoaded);
        var hasInFlightHouseholdApplication = household.Applications.Any(IsInFlightHouseholdApplication);
        var hasPendingCase = household.SummerEbtCases.Any(IsPendingApplicant);

        return hasNonCoLoaded || hasInFlightHouseholdApplication || hasPendingCase
            ? CoLoadedCohort.MixedOrApplicantExcluded
            : CoLoadedCohort.CoLoadedOnly;
    }

    /// <summary>
    /// Maps a default off-boarding reason to the co-loaded-only screen when the household
    /// cohort warrants it.
    /// </summary>
    public static string ResolveOffboardingReason(string defaultReason, HouseholdData? household) =>
        Classify(household) == CoLoadedCohort.CoLoadedOnly
            ? "coLoadedOnly"
            : defaultReason;

    private static bool IsPendingApplicant(SummerEbtCase summerEbtCase) =>
        summerEbtCase.ApplicationStatus is ApplicationStatus.Pending or ApplicationStatus.UnderReview;

    private static bool IsInFlightHouseholdApplication(Application application) =>
        application.ApplicationStatus is ApplicationStatus.Pending or ApplicationStatus.UnderReview;
}
