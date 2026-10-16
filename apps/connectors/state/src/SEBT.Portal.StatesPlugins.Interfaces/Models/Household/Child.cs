namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Represents a child on a benefit application.
/// </summary>
public class Child
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Unknown;

    /// <summary>
    /// The state system's identifier for this child for the season, so a child known only
    /// through a pending application can still be identified.
    /// </summary>
    public string? SourceChildId { get; set; }
}
