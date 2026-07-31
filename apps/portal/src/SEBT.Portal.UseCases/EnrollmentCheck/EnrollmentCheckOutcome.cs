namespace SEBT.Portal.UseCases.EnrollmentCheck;

/// <summary>
/// One entry per surfaced child plus the backend's optional result-level <see cref="Message"/>.
/// Child identity always comes from the submitted command — never from a state backend — so no
/// state-system PII can surface.
/// </summary>
public sealed record EnrollmentCheckOutcome(
    IReadOnlyList<EnrollmentChildOutcome> Results, string? Message = null);

/// <summary>
/// <see cref="CheckId"/> is the handler-minted correlation id; <see cref="MatchConfidence"/> and
/// <see cref="StatusMessage"/> ride along when the backend supplies them.
/// </summary>
public sealed record EnrollmentChildOutcome(
    Guid CheckId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    bool IsMatch,
    double? MatchConfidence = null,
    string? StatusMessage = null);
