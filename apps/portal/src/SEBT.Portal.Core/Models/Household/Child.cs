namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents a child on a benefit application.
/// </summary>
public class Child
{
    /// <summary>
    /// The child's first name.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// The child's last name.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// The application status for this child.
    /// </summary>
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Unknown;
}
