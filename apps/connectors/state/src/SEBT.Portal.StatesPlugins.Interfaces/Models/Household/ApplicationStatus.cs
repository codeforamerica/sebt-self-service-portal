namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Represents the status of a benefit application.
/// </summary>
public enum ApplicationStatus
{
    Unknown = 0,
    Pending = 1,
    Approved = 2,
    Denied = 3,
    UnderReview = 4,
    Cancelled = 5
}
