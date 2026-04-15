using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Api.Models.Household;

/// <summary>
/// Request model for requesting replacement cards for one or more cases.
/// </summary>
public record RequestCardReplacementRequest
{
    /// <summary>Case IDs identifying which cards to replace.</summary>
    [Required(ErrorMessage = "At least one case ID is required.")]
    [MinLength(1, ErrorMessage = "At least one case ID is required.")]
    public required List<string> CaseIds { get; init; }
}
