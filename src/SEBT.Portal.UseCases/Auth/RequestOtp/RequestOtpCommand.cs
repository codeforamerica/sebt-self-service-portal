using System.ComponentModel.DataAnnotations;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Auth;
/// <summary>
/// Command to request a one-time password (OTP).
/// </summary>
public class RequestOtpCommand : ICommand
{
    /// <summary>
    /// The email address to which the OTP will be sent.
    /// </summary>
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; init; } = string.Empty;
}