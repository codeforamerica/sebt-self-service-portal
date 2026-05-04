namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents a benefit application with its associated children and card information.
/// This is a domain model used for in-memory storage via MockHouseholdRepository.
/// Application data is stored as part of HouseholdData.Applications list.
/// </summary>
public class Application
{
    /// <summary>
    /// The application number.
    /// </summary>
    public string? ApplicationNumber { get; set; }

    /// <summary>
    /// The case number associated with this application.
    /// </summary>
    public string? CaseNumber { get; set; }

    /// <summary>
    /// The status of the application.
    /// </summary>
    public ApplicationStatus ApplicationStatus { get; set; } = ApplicationStatus.Unknown;

    /// <summary>
    /// The date the application was submitted.
    /// </summary>
    public DateTime? ApplicationDate { get; set; }

    /// <summary>
    /// The date when the benefit was issued for this application.
    /// </summary>
    public DateTime? BenefitIssueDate { get; set; }

    /// <summary>
    /// The date when the benefit expires for this application.
    /// </summary>
    public DateTime? BenefitExpirationDate { get; set; }

    /// <summary>
    /// The last 4 digits of the card the benefit is issued to for this application.
    /// </summary>
    public string? Last4DigitsOfCard { get; set; }

    /// <summary>
    /// The list of children on this application.
    /// </summary>
    public List<Child> Children { get; set; } = new();

    /// <summary>
    /// The number of children on this application.
    /// </summary>
    public int ChildrenOnApplication => Children.Count;

    /// <summary>
    /// The type of issuance for this application.
    /// </summary>
    public IssuanceType IssuanceType { get; set; } = IssuanceType.Unknown;
}
