namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents a household member's Summer EBT case from the state backend.
/// Per-child case data including eligibility, card, and benefit information.
/// </summary>
public class SummerEbtCase
{
    /// <summary>
    /// The Summer EBT case identifier.
    /// </summary>
    public string? SummerEBTCaseID { get; set; }

    /// <summary>
    /// The application ID for this case.
    /// </summary>
    public string? ApplicationId { get; set; }

    /// <summary>
    /// The application student ID.
    /// </summary>
    public string? ApplicationStudentId { get; set; }

    /// <summary>
    /// The child's first name.
    /// </summary>
    public string ChildFirstName { get; set; } = string.Empty;

    /// <summary>
    /// The child's last name.
    /// </summary>
    public string ChildLastName { get; set; } = string.Empty;

    /// <summary>
    /// The child's date of birth.
    /// </summary>
    public DateTime? ChildDateOfBirth { get; set; }

    /// <summary>
    /// The household type.
    /// </summary>
    public string HouseholdType { get; set; } = string.Empty;

    /// <summary>
    /// The eligibility type.
    /// </summary>
    public string EligibilityType { get; set; } = string.Empty;

    /// <summary>
    /// The source of eligibility (e.g. school, SNAP, TANF).
    /// </summary>
    public string? EligibilitySource { get; set; }

    /// <summary>
    /// The type of issuance for this case (e.g. auto-issuance vs application-based).
    /// </summary>
    public IssuanceType IssuanceType { get; set; } = IssuanceType.Unknown;

    /// <summary>
    /// The application date.
    /// </summary>
    public DateTime? ApplicationDate { get; set; }

    /// <summary>
    /// The status of the application.
    /// </summary>
    public ApplicationStatus ApplicationStatus { get; set; } = ApplicationStatus.Unknown;

    /// <summary>
    /// The mailing address for this case.
    /// </summary>
    public Address? MailingAddress { get; set; }

    /// <summary>
    /// The EBT case number.
    /// </summary>
    public string? EbtCaseNumber { get; set; }

    /// <summary>
    /// The last 4 digits of the EBT card.
    /// </summary>
    public string? EbtCardLastFour { get; set; }

    /// <summary>
    /// The EBT card status.
    /// </summary>
    public string? EbtCardStatus { get; set; }

    /// <summary>
    /// The EBT card issue date.
    /// </summary>
    public DateTime? EbtCardIssueDate { get; set; }

    /// <summary>
    /// The EBT card balance.
    /// </summary>
    public decimal? EbtCardBalance { get; set; }

    /// <summary>
    /// When a card replacement was last requested for this case.
    /// Used to enforce a cooldown period between replacement requests.
    /// </summary>
    public DateTime? CardRequestedAt { get; set; }

    /// <summary>
    /// The date benefits become available.
    /// </summary>
    public DateTime? BenefitAvailableDate { get; set; }

    /// <summary>
    /// The date benefits expire.
    /// </summary>
    public DateTime? BenefitExpirationDate { get; set; }
}
