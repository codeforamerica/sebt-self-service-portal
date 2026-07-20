namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Represents a benefit application with its associated children and card information.
/// </summary>
public class Application
{
    public string? ApplicationNumber { get; set; }
    public string? CaseNumber { get; set; }
    public ApplicationStatus ApplicationStatus { get; set; } = ApplicationStatus.Unknown;
    public DateTime? ApplicationDate { get; set; }
    public DateTime? BenefitIssueDate { get; set; }
    public DateTime? BenefitExpirationDate { get; set; }
    public List<Child> Children { get; set; } = new();
    public int ChildrenOnApplication => Children.Count;
    public IssuanceType IssuanceType { get; set; } = IssuanceType.Unknown;
}
