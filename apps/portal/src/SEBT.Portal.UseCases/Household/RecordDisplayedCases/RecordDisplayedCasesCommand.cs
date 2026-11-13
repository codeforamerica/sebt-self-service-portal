using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Command to record which Summer EBT cases were displayed to an authenticated portal user.
/// </summary>
public class RecordDisplayedCasesCommand : ICommand
{
    /// <summary>
    /// The authenticated user's claims principal, used to resolve the portal user ID.
    /// </summary>
    [Required]
    public required ClaimsPrincipal User { get; init; }

    /// <summary>
    /// The cases returned to the user; cases without a case ID are ignored.
    /// </summary>
    [Required]
    public required IReadOnlyList<SummerEbtCase> Cases { get; init; }
}
