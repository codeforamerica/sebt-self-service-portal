using System.ComponentModel.DataAnnotations;
using SEBT.Portal.Kernel;

public class ValidateOtpCommand : ICommand
{
    /// <summary>
    /// The email address associated with the OTP.
    /// </summary>
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// The one-time password (OTP) to validate.
    /// </summary>
    [Required(ErrorMessage = "One time password is required.")]
    [Length(6, 6, ErrorMessage = "One time password must be exactly six characters.")]
    public string Otp { get; init; } = string.Empty;
}