namespace SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

public class EnrollmentCheckRequest
{
    public required IList<ChildCheckRequest> Children { get; init; }
    public string? GuardianContactInfo { get; init; }
}
