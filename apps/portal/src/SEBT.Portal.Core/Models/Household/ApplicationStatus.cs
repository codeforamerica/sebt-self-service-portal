namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents the status of a benefit application.
///
/// Serialized as its integer value over the API (no JsonStringEnumConverter, unlike CardStatus),
/// so these ordinals are the wire contract. The frontend decodes them against a hardcoded table
/// in features/household/api/schema.ts, and the plugin boundary casts between this enum and the
/// state-connector's mirror by value, not by name. Do not reorder or renumber members; append
/// only. EnumWireContractTests and EnumParityTests fail if you do.
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
