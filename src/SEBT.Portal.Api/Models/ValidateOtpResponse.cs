namespace SEBT.Portal.Api.Models;

/// <summary>
/// Response model for successful OTP validation or token refresh: JWT plus optional client routing.
/// </summary>
/// <param name="Token">The JWT token for authenticated access.</param>
/// <param name="RequiresIdProofing">When true, the client should send the user through ID proofing (Socure).</param>
public record ValidateOtpResponse(string Token, bool RequiresIdProofing);

