namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Result of evaluating whether ID proofing may proceed toward Socure.
/// </summary>
/// <param name="CanProceedToSocure">
/// False when eligibility gating is on and there is no qualifying household;
/// otherwise true (including when the gate is disabled in configuration).
/// </param>
public record SocureEligibilityResponse(bool CanProceedToSocure);
