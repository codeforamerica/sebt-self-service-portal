using SEBT.Portal.Core.Models.EnrollmentCheck;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Logs de-identified enrollment check submissions for analytics purposes.
/// No PII (names, full dates of birth) is stored -- only birth year,
/// enrollment status, eligibility type, and school name.
/// </summary>
public interface IEnrollmentCheckSubmissionLogger
{
    Task LogSubmissionAsync(
        EnrollmentCheckSubmission submission,
        CancellationToken cancellationToken = default);
}
