using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Api.Models.Household;

/// <summary>
/// Request model for requesting replacement cards for one or more cases.
/// </summary>
public record RequestCardReplacementRequest
{
    /// <summary>Case references identifying which cards to replace.</summary>
    [Required(ErrorMessage = "At least one case reference is required.")]
    [MinLength(1, ErrorMessage = "At least one case reference is required.")]
    public required List<CaseRefRequestDto> CaseRefs { get; init; }
}

/// <summary>
/// Wire shape of a single case reference. Mirrors the use-cases <c>CaseRefDto</c>
/// and the state-connector <c>CaseRef</c>.
/// </summary>
public record CaseRefRequestDto
{
    /// <summary>Primary case identifier (from <c>SummerEbtCase.summerEBTCaseID</c>).</summary>
    [Required(ErrorMessage = "summerEbtCaseId is required for each case reference.")]
    public required string SummerEbtCaseId { get; init; }

    /// <summary>Application identifier when the case is application-based; null for auto-eligible cases.</summary>
    public string? ApplicationId { get; init; }

    /// <summary>Per-(case, child) identifier when the case is application-based; null for auto-eligible cases.</summary>
    public string? ApplicationStudentId { get; init; }
}
