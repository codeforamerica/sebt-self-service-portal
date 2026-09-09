namespace SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

public class ChildCheckRequest
{
    public required Guid CheckId { get; init; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public string? SchoolName { get; init; }
    public string? SchoolCode { get; init; }
    public IDictionary<string, string> AdditionalFields { get; init; } = new Dictionary<string, string>();
}
