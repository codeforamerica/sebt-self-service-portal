using SEBT.Portal.StatesPlugins.Interfaces.Data.Cases;

namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Represents household data including Summer EBT cases, applications, and benefit information.
/// State plugins return this schema so the main portal can map once at the boundary.
/// </summary>
public class HouseholdData
{
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public List<SummerEbtCase> SummerEbtCases { get; set; } = new();
    public List<Application> Applications { get; set; } = new();
    public Address? AddressOnFile { get; set; }
    public UserProfile? UserProfile { get; set; }
    public BenefitIssuanceType BenefitIssuanceType { get; set; } = BenefitIssuanceType.Unknown;
}
