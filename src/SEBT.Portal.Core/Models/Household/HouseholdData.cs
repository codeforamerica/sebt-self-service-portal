namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents household data including application and benefit information.
/// This domain model is used for in-memory storage via MockHouseholdRepository.
/// All household and application data is stored in-memory during development.
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
    /// The list of applications for this household.
    /// Each application can have its own children, status, and card information.
    /// </summary>
    public List<Application> Applications { get; set; } = new();

    /// <summary>
    /// The address on file. This should only be populated if ID verification is completed.
    /// </summary>
    public Address? AddressOnFile { get; set; }

    /// <summary>
    /// The logged-in user's profile (first, middle, last name)
    /// </summary>
    public UserProfile? UserProfile { get; set; }

    /// <summary>
    /// The type of benefit issuance for this household.
    /// </summary>
    public BenefitIssuanceType BenefitIssuanceType { get; set; } = BenefitIssuanceType.Unknown;
}
