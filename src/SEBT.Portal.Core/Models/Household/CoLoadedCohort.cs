namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Classifies a household relative to co-loaded benefits, for use by the portal's
/// exclusion logic and by analytics to segment usage.
///
/// The classification is derived at runtime from the pre-filter household state
/// (case list + applications), using the rule:
/// <list type="bullet">
///   <item><description><see cref="NonCoLoaded"/> — no <c>SummerEbtCase</c> is co-loaded.</description></item>
///   <item><description><see cref="CoLoadedOnly"/> — every case is co-loaded AND there are no applications and no pending-status cases.</description></item>
///   <item><description><see cref="MixedOrApplicantExcluded"/> — the household has at least one co-loaded case AND at least one of: a non-co-loaded case, an <c>Application</c> record, or a case whose <c>ApplicationStatus</c> is pending/under review.</description></item>
/// </list>
///
/// The <see cref="MixedOrApplicantExcluded"/> cohort is the "excluded cohort" for
/// co-loaded cases are stripped from their dashboard/benefits response so
/// they see only their non-co-loaded view (or their applications).
/// </summary>
public enum CoLoadedCohort
{
    /// <summary>
    /// Household has no co-loaded cases. Full portal experience available.
    /// </summary>
    NonCoLoaded = 0,

    /// <summary>
    /// Household's cases are all co-loaded; no non-co-loaded view exists and
    /// the household has no pending applications that would justify showing
    /// an applicant experience. Cases remain visible so the user sees
    /// something; per-case flags deny self-service actions.
    /// </summary>
    CoLoadedOnly = 1,

    /// <summary>
    /// Mixed-eligibility family, or applicant with co-loaded benefits.
    /// Co-loaded cases are suppressed from the response so the user sees only
    /// their non-co-loaded cases and/or applications.
    /// </summary>
    MixedOrApplicantExcluded = 2
}
