using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Command to request replacement cards for one or more applications.
/// </summary>
public class RequestCardReplacementCommand : ICommand
{
    /// <summary>
    /// The authenticated user's claims principal, used to resolve household identity.
    /// </summary>
    [Required]
    public required ClaimsPrincipal User { get; init; }

    /// <summary>
    /// Application numbers identifying which cards to replace.
    /// All children on a selected application share the same card.
    /// </summary>
    [Required(ErrorMessage = "At least one application number is required.")]
    [MinLength(1, ErrorMessage = "At least one application number is required.")]
    public required List<string> ApplicationNumbers { get; init; }
}
