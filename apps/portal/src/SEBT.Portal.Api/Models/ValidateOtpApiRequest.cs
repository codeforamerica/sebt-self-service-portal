namespace SEBT.Portal.Api.Models;

/// <summary>
/// Request body for validating a one-time password (OTP) for user authentication.
/// </summary>
public record ValidateOtpApiRequest(string Email, string Otp);
