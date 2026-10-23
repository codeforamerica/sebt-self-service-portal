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

    /// <summary>
    /// The state system's identifier for this child for the season, so a child known only
    /// through a pending application can still be identified.
    /// </summary>
    public string? SourceChildId { get; set; }
}
