namespace SEBT.Portal.Api.Models;

/// <summary>
/// Request body for requesting a one-time password (OTP) to be sent to an email address.
/// </summary>
public record RequestOtpApiRequest(string Email);
