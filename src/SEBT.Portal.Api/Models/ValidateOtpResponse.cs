namespace SEBT.Portal.Api.Models;

/// <summary>
/// Response model for successful OTP validation containing the JWT authentication token
/// and routing information for the frontend.
/// </summary>
/// <param name="Token">The JWT token for authenticated access.</param>
/// <param name="RequiresIdProofing">
/// True if the user must complete ID proofing before accessing the portal.
/// False if the user can proceed directly to the dashboard.
/// </param>
public record ValidateOtpResponse(string Token, bool RequiresIdProofing);

