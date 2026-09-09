namespace SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

public class EnrollmentCheckResult
{
    public required IList<ChildCheckResult> Results { get; init; }
    public string? ResponseMessage { get; init; }
}
