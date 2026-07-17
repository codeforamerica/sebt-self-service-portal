namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents the status of a benefit application.
/// </summary>
public enum ApplicationStatus
{
    /// <summary>
    /// Application status is unknown or not set.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Application is pending review.
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Application has been approved.
    /// </summary>
    Approved = 2,

    /// <summary>
    /// Application has been denied.
    /// </summary>
    Denied = 3,

    /// <summary>
    /// Application is under review.
    /// </summary>
    UnderReview = 4,

    /// <summary>
    /// Application has been cancelled.
    /// </summary>
    Cancelled = 5
}
