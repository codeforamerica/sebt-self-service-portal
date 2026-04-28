namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents household data including application and benefit information.
/// This domain model is used for in-memory storage via MockHouseholdRepository.
/// All household and application data is stored in-memory during development.
/// </summary>
public record HouseholdData
{
    /// <summary>
    /// The email address on file for the household.
    /// Null when excluded due to ID proofing requirements.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// The phone number on file for the household.
    /// </summary>
    public string? Phone { get; set; }

    /// <summary>
    /// The list of Summer EBT cases (per-child) for this household.
    /// </summary>
    public List<SummerEbtCase> SummerEbtCases { get; set; } = new();

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
    /// The type of benefit issuance for this household, reflecting the cases the
    /// client will receive. Plugin-sourced at load time; the query handler may
    /// realign this value when it filters the case list (e.g. a mixed-eligibility
    /// household whose co-loaded cases are filtered out becomes <c>SummerEbt</c>)
    /// so that downstream routing keyed on this field matches the visible view.
    /// </summary>
    public BenefitIssuanceType BenefitIssuanceType { get; set; } = BenefitIssuanceType.Unknown;

    /// <summary>
    /// Computed permissions for self-service portal actions.
    /// Set by the query handler after evaluating config rules against household data.
    /// </summary>
    public AllowedActions? AllowedActions { get; set; }
}
