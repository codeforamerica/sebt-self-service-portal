namespace SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

public class ChildCheckResult
{
    public required Guid CheckId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public required EnrollmentStatus Status { get; init; }
    public double? MatchConfidence { get; init; }
    public string? StatusMessage { get; init; }
    public EligibilityType? EligibilityType { get; init; }
    public string? SchoolName { get; init; }
    public IDictionary<string, object> Details { get; init; } = new Dictionary<string, object>();
}
