namespace SEBT.Portal.Core.StateBackends;

public interface IEnrollmentCheckBackend
{
    Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request, CancellationToken cancellationToken = default);
}

/// <summary>An enrollment-eligibility check for a batch of children.</summary>
public sealed record EnrollmentCheckRequest(IReadOnlyList<EnrollmentChild> Children);

/// <summary>
/// One child to check. <see cref="CheckId"/> is echoed on the result for correlation;
/// <see cref="SchoolIdentifier"/> is the state's school name or code, when the portal has it.
/// </summary>
public sealed record EnrollmentChild(
    string CheckId, string FirstName, string LastName, DateOnly DateOfBirth, string? SchoolIdentifier = null);

/// <summary>
/// Per-child outcomes, one per requested child in request order. <see cref="Message"/> is the
/// backend's result-level status text, when a <c>messageField</c> is configured.
/// </summary>
public sealed record EnrollmentCheckResult(
    IReadOnlyList<EnrollmentChildResult> Results, string? Message = null);

/// <summary>
/// <see cref="MatchConfidence"/> is the winning row's confidenceThreshold score, reported even below
/// the threshold (null under other strategies). <see cref="StatusMessage"/> is the winning row's
/// status text, when a <c>statusMessageField</c> is configured.
/// </summary>
public sealed record EnrollmentChildResult(
    string CheckId, bool IsMatch, double? MatchConfidence = null, string? StatusMessage = null);
