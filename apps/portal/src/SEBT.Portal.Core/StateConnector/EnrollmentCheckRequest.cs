namespace SEBT.Portal.Core.StateConnector;

public class EnrollmentCheckRequest
{
    public required IList<ChildCheckRequest> Children { get; init; }
    public string? GuardianContactInfo { get; init; }
}
