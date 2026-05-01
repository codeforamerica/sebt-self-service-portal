namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Classifies a household relative to co-loaded benefits, for use by the portal's
/// exclusion logic and by analytics to segment usage.
///
/// The classification is derived at runtime from the pre-filter household state
/// (case list + applications), using the rule:
/// <list type="bullet">
///   <item><description><see cref="NonCoLoaded"/> — no <c>SummerEbtCase</c> is co-loaded.</description></item>
///   <item><description><see cref="CoLoadedOnly"/> — every case is co-loaded AND there are no in-flight household applications (<c>Pending</c>/<c>UnderReview</c> on <see cref="HouseholdData.Applications"/>), no cases in pending applicant status (same statuses on <see cref="SummerEbtCase.ApplicationStatus"/>).</description></item>
///   <item><description><see cref="MixedOrApplicantExcluded"/> — the household has at least one co-loaded case AND at least one of: a non-co-loaded case, an in-flight household application (<c>Pending</c>/<c>UnderReview</c>), or a case whose <see cref="SummerEbtCase.ApplicationStatus"/> is pending or under review.</description></item>
/// </list>
///
/// For households in <see cref="MixedOrApplicantExcluded"/>, co-loaded benefits may be suppressed from their dashboard/benefits response so they see only non-co-loaded cases and/or application views (subject to <c>CoLoadedCohortFilter</c> configuration).
/// </summary>
public enum CoLoadedCohort
{
    /// <summary>
    /// Household has no co-loaded cases. Full portal experience available.
    /// </summary>
    NonCoLoaded = 0,

    /// <summary>
    /// Household's cases are all co-loaded; no non-co-loaded view exists and
    /// there is no in-flight applicant journey (no pending/under-review household
    /// <see cref="HouseholdData.Applications"/> and no pending applicant cases).
    /// Cases remain visible so the user sees something; per-case flags deny self-service actions.
    /// </summary>
    CoLoadedOnly = 1,

    /// <summary>
    /// Mixed-eligibility family, or applicant with co-loaded benefits.
    /// Co-loaded cases are suppressed from the response so the user sees only
    /// their non-co-loaded cases and/or applications.
    /// </summary>
    MixedOrApplicantExcluded = 2
}
