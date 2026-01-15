namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents household data including application and benefit information.
/// </summary>
public class HouseholdData
{
    /// <summary>
    /// The email address on file for the household.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The phone number on file for the household.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// The list of children on the application.
    /// </summary>
    public List<Child> Children { get; set; } = new();

    /// <summary>
    /// The date when the benefit was issued.
    /// </summary>
    public DateTime? BenefitIssueDate { get; set; }

    /// <summary>
    /// The date when the benefit expires.
    /// </summary>
    public DateTime? BenefitExpirationDate { get; set; }

    /// <summary>
    /// The last 4 digits of the card the benefit is issued to.
    /// </summary>
    public string? Last4DigitsOfCard { get; set; }

    /// <summary>
    /// The application number.
    /// </summary>
    public string? ApplicationNumber { get; set; }

    /// <summary>
    /// The case number.
    /// </summary>
    public string? CaseNumber { get; set; }

    /// <summary>
    /// The status of the application.
    /// </summary>
    public ApplicationStatus ApplicationStatus { get; set; } = ApplicationStatus.Unknown;

    /// <summary>
    /// The number of children on the application.
    /// </summary>
    public int ChildrenOnApplication => Children.Count;

    /// <summary>
    /// The address on file. This should only be populated if ID verification is completed.
    /// </summary>
    public Address? AddressOnFile { get; set; }
}
