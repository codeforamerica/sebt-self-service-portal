using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Command to request a replacement card for an authenticated user's application.
/// </summary>
public class RequestCardReplacementCommand : ICommand
{
    /// <summary>
    /// The authenticated user's claims principal, used to resolve household identity.
    /// </summary>
    [Required]
    public required ClaimsPrincipal User { get; init; }

    /// <summary>
    /// The application number identifying which card to replace.
    /// </summary>
    [Required(ErrorMessage = "Application number is required.")]
    public required string ApplicationNumber { get; init; }
}
