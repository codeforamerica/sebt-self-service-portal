namespace SEBT.Portal.Infrastructure.Data.Entities;

/// <summary>
/// Entity model for storing household data including application and benefit information.
/// </summary>
public class HouseholdEntity
{
    /// <summary>
    /// Primary key identifier.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The email address of the household.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// The phone number on file for the household.
    /// </summary>
    public string? Phone { get; set; }

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
    /// The status of the application (0 = Unknown, 1 = Pending, 2 = Approved, etc.).
    /// </summary>
    public int ApplicationStatus { get; set; } = 0; // 0 = Unknown

    /// <summary>
    /// The status of the card (0 = Requested, 1 = Mailed, 2 = Active, 3 = Deactivated).
    /// </summary>
    public int CardStatus { get; set; } = 0; // 0 = Requested

    /// <summary>
    /// The date and time when the card status was set to Requested.
    /// </summary>
    public DateTime? CardRequestedAt { get; set; }

    /// <summary>
    /// The date and time when the card status was set to Mailed.
    /// </summary>
    public DateTime? CardMailedAt { get; set; }

    /// <summary>
    /// The date and time when the card status was set to Active.
    /// </summary>
    public DateTime? CardActivatedAt { get; set; }

    /// <summary>
    /// The date and time when the card status was set to Deactivated.
    /// </summary>
    public DateTime? CardDeactivatedAt { get; set; }

    /// <summary>
    /// The date and time when the household record was first created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The date and time when the household record was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property to children on the application.
    /// </summary>
    public List<ChildEntity> Children { get; set; } = new();

    /// <summary>
    /// Navigation property to the address on file.
    /// </summary>
    public AddressEntity? Address { get; set; }
}
