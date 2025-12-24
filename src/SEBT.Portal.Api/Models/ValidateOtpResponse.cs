namespace SEBT.Portal.Api.Models;

/// <summary>
/// Response model for successful OTP validation containing the JWT authentication token.
/// </summary>
/// <param name="Token">The JWT token for authenticated access.</param>
public record ValidateOtpResponse(string Token);

