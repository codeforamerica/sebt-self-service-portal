using System.ComponentModel.DataAnnotations;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Auth;

/// <summary>
/// Command for refreshing a JWT token with updated user information.
/// </summary>
public class RefreshTokenCommand : ICommand<string>
{
    /// <summary>
    /// The email address of the user requesting the token refresh.
    /// </summary>
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    public string Email { get; init; } = string.Empty;
}

