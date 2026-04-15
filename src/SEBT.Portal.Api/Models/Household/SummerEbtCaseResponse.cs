extern alias Core;

namespace SEBT.Portal.Api.Models.Household;

using ApplicationStatus = Core::SEBT.Portal.Core.Models.Household.ApplicationStatus;

/// <summary>
/// API response model for a Summer EBT case (per-child).
/// </summary>
public record SummerEbtCaseResponse
{
    /// <summary>
    /// The Summer EBT case identifier.
    /// </summary>
    public string? SummerEBTCaseID { get; init; }

    /// <summary>
    /// The application ID for this case.
    /// </summary>
    public string? ApplicationId { get; init; }

    /// <summary>
    /// The application student ID.
    /// </summary>
    public string? ApplicationStudentId { get; init; }

    /// <summary>
    /// The child's first name.
    /// </summary>
    public string ChildFirstName { get; init; } = string.Empty;

    /// <summary>
    /// The child's last name.
    /// </summary>
    public string ChildLastName { get; init; } = string.Empty;

    /// <summary>
    /// The child's date of birth.
    /// </summary>
    public DateTime? ChildDateOfBirth { get; init; }

    /// <summary>
    /// The household type.
    /// </summary>
    public string HouseholdType { get; init; } = string.Empty;

    /// <summary>
    /// The eligibility type.
    /// </summary>
    public string EligibilityType { get; init; } = string.Empty;

    /// <summary>
    /// The source of eligibility (e.g. school, SNAP, TANF).
    /// </summary>
    public string? EligibilitySource { get; init; }

    /// <summary>
    /// The type of issuance for this case (e.g. auto-issuance vs application-based).
    /// </summary>
    public Core::SEBT.Portal.Core.Models.Household.IssuanceType IssuanceType { get; init; }

    /// <summary>
    /// The application date.
    /// </summary>
    public DateTime? ApplicationDate { get; init; }

    /// <summary>
    /// The status of the application.
    /// </summary>
    public ApplicationStatus ApplicationStatus { get; init; }

    /// <summary>
    /// The mailing address for this case.
    /// </summary>
    public AddressResponse? MailingAddress { get; init; }

    /// <summary>
    /// The EBT case number.
    /// </summary>
    public string? EbtCaseNumber { get; init; }

    /// <summary>
    /// The last 4 digits of the EBT card.
    /// </summary>
    public string? EbtCardLastFour { get; init; }

    /// <summary>
    /// The EBT card status.
    /// </summary>
    public string? EbtCardStatus { get; init; }

    /// <summary>
    /// The EBT card issue date.
    /// </summary>
    public DateTime? EbtCardIssueDate { get; init; }

    /// <summary>
    /// The EBT card balance.
    /// </summary>
    public decimal? EbtCardBalance { get; init; }

    /// <summary>
    /// When a card replacement was last requested for this case.
    /// </summary>
    public DateTime? CardRequestedAt { get; init; }

    /// <summary>
    /// The date benefits become available.
    /// </summary>
    public DateTime? BenefitAvailableDate { get; init; }

    /// <summary>
    /// The date benefits expire.
    /// </summary>
    public DateTime? BenefitExpirationDate { get; init; }
}
