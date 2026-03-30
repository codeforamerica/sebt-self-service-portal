using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Api.Models.Household;

/// <summary>
/// Request model for requesting a replacement card.
/// </summary>
public record RequestCardReplacementRequest
{
    /// <summary>The application number identifying which card to replace.</summary>
    [Required(ErrorMessage = "Application number is required.")]
    public required string ApplicationNumber { get; init; }
}
