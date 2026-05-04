extern alias Core;

namespace SEBT.Portal.Api.Models.Household;

using ApplicationStatus = Core::SEBT.Portal.Core.Models.Household.ApplicationStatus;
using IssuanceType = Core::SEBT.Portal.Core.Models.Household.IssuanceType;

/// <summary>
/// API response model for a benefit application.
/// </summary>
public record ApplicationResponse
{
    /// <summary>
    /// The application number.
    /// </summary>
    public string? ApplicationNumber { get; init; }

    /// <summary>
    /// The case number associated with this application.
    /// </summary>
    public string? CaseNumber { get; init; }

    /// <summary>
    /// The status of the application.
    /// </summary>
    public ApplicationStatus ApplicationStatus { get; init; }

    /// <summary>
    /// The date the application was submitted.
    /// </summary>
    public DateTime? ApplicationDate { get; init; }

    /// <summary>
    /// The date when the benefit was issued for this application.
    /// </summary>
    public DateTime? BenefitIssueDate { get; init; }

    /// <summary>
    /// The date when the benefit expires for this application.
    /// </summary>
    public DateTime? BenefitExpirationDate { get; init; }

    /// <summary>
    /// The last 4 digits of the card the benefit is issued to for this application.
    /// </summary>
    public string? Last4DigitsOfCard { get; init; }

    /// <summary>
    /// The list of children on this application.
    /// </summary>
    public IReadOnlyList<ChildResponse> Children { get; init; } = Array.Empty<ChildResponse>();

    /// <summary>
    /// The number of children on this application.
    /// </summary>
    public int ChildrenOnApplication { get; init; }

    /// <summary>
    /// The type of issuance for this application.
    /// </summary>
    public IssuanceType IssuanceType { get; init; }
}
