namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Represents a child on a benefit application.
/// </summary>
public class Child
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Unknown;
}
