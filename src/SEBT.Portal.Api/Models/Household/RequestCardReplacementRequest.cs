using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Api.Models.Household;

/// <summary>
/// Request model for requesting replacement cards for one or more applications.
/// </summary>
public record RequestCardReplacementRequest
{
    /// <summary>Application numbers identifying which cards to replace.</summary>
    [Required(ErrorMessage = "At least one application number is required.")]
    [MinLength(1, ErrorMessage = "At least one application number is required.")]
    public required List<string> ApplicationNumbers { get; init; }
}
