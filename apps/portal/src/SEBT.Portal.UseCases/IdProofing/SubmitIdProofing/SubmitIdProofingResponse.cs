namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Response from ID proofing submission. Matches the frontend contract from DC-137.
/// </summary>
/// <param name="Result">
/// One of: "matched" (user verified), "failed" (verification failed),
/// or "documentVerificationRequired" (user must upload documents).
/// </param>
/// <param name="ChallengeId">
/// Present when Result is "documentVerificationRequired".
/// Opaque GUID used to start the document capture flow.
/// </param>
/// <param name="AllowIdRetry">
/// Whether the user can retry with a different ID number.
/// Derived server-side, never from URL params.
/// </param>
/// <param name="CanApply">
/// Whether the user can still apply despite failing verification.
/// </param>
/// <param name="OffboardingReason">
/// Reason code for off-boarding (e.g., "noIdProvided", "idProofingFailed").
/// </param>
public record SubmitIdProofingResponse(
    string Result,
    Guid? ChallengeId = null,
    bool? AllowIdRetry = null,
    bool? CanApply = null,
    string? OffboardingReason = null);
