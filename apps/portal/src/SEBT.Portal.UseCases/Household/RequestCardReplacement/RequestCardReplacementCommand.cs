using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Portal-side reference to a specific case for the card-replacement command.
/// Mirrors the state-connector <c>CaseRef</c> but lives in the use-cases layer
/// so inner layers do not depend on plugin contracts.
/// </summary>
public record CaseRefDto(string SummerEbtCaseId, string? ApplicationId, string? ApplicationStudentId);

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
    /// Case references identifying which cards to replace.
    /// Each reference carries the primary <c>SummerEbtCaseId</c> plus optional
    /// <c>ApplicationId</c> / <c>ApplicationStudentId</c> for application-based cases.
    /// </summary>
    [Required(ErrorMessage = "At least one case reference is required.")]
    [MinLength(1, ErrorMessage = "At least one case reference is required.")]
    public required List<CaseRefDto> CaseRefs { get; init; }
}
