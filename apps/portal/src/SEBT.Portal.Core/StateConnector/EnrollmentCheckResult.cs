namespace SEBT.Portal.Core.StateConnector;

public class EnrollmentCheckResult
{
    public required IList<ChildCheckResult> Results { get; init; }
    public string? ResponseMessage { get; init; }
}
