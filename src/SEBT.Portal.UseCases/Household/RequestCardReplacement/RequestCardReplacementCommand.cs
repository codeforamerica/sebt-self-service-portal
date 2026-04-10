using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Command to request replacement cards for one or more cases.
/// </summary>
public class RequestCardReplacementCommand : ICommand
{
    /// <summary>
    /// The authenticated user's claims principal, used to resolve household identity.
    /// </summary>
    [Required]
    public required ClaimsPrincipal User { get; init; }

    /// <summary>
    /// Case IDs identifying which cards to replace.
    /// Each case represents an enrolled child with a card.
    /// </summary>
    [Required(ErrorMessage = "At least one case ID is required.")]
    [MinLength(1, ErrorMessage = "At least one case ID is required.")]
    public required List<string> CaseIds { get; init; }
}
