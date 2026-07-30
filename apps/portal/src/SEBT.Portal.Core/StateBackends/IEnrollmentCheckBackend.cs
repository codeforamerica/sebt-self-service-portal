namespace SEBT.Portal.Core.StateBackends;

public interface IEnrollmentCheckBackend
{
    Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request, CancellationToken cancellationToken = default);
}

/// <summary>An enrollment-eligibility check for a batch of children.</summary>
public sealed record EnrollmentCheckRequest(IReadOnlyList<EnrollmentChild> Children);

/// <summary>
/// One child to check. <see cref="CheckId"/> is an opaque caller correlation id echoed on the result.
/// <see cref="SchoolName"/> is the state's school identifier for enrollment matching — optional
/// because the portal doesn't always have it.
/// </summary>
public sealed record EnrollmentChild(
    string CheckId, string FirstName, string LastName, DateOnly DateOfBirth, string? SchoolName = null);

/// <summary>
/// The per-child enrollment outcomes, one entry per requested child, in request order.
/// <see cref="Message"/> is the backend's result-level status text (e.g. CBMS's response-root
/// message), populated only when the response mapping configures a <c>messageField</c>.
/// </summary>
public sealed record EnrollmentCheckResult(
    IReadOnlyList<EnrollmentChildResult> Results, string? Message = null);

/// <summary>
/// One child's enrollment outcome. <see cref="IsMatch"/> is the fan-in verdict: true when any of the
/// child's candidate request rows produced a matching backend row. <see cref="MatchConfidence"/> is
/// the winning row's score under the confidenceThreshold strategy — populated even below the
/// threshold so callers can surface the score that was computed; null under other strategies.
/// <see cref="StatusMessage"/> is the winning row's status text when a <c>statusMessageField</c>
/// is configured.
/// </summary>
public sealed record EnrollmentChildResult(
    string CheckId, bool IsMatch, double? MatchConfidence = null, string? StatusMessage = null);
