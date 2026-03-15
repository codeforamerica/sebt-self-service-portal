namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Response from checking verification status. Matches the frontend contract from DC-137.
/// </summary>
/// <param name="Status">One of: "pending", "verified", "rejected".</param>
/// <param name="AllowIdRetry">Whether the user can retry with a different ID (server-derived).</param>
/// <param name="OffboardingReason">Present when status is "rejected".</param>
public record VerificationStatusResponse(
    string Status,
    bool? AllowIdRetry = null,
    string? OffboardingReason = null);
