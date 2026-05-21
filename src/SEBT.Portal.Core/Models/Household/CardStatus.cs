namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents the status of a benefit card.
/// </summary>
public enum CardStatus
{
    /// <summary>
    /// Card has been requested but not yet mailed.
    /// </summary>
    Requested = 0,

    /// <summary>
    /// Card has been mailed to the recipient.
    /// </summary>
    Mailed = 1,

    /// <summary>
    /// Card is active and can be used.
    /// </summary>
    Active = 2,

    /// <summary>
    /// Card has been deactivated and cannot be used.
    /// </summary>
    Deactivated = 3,

    /// <summary>
    /// Card status is unknown or not set.
    /// </summary>
    Unknown = 4,

    /// <summary>
    /// Card has been processed but not yet mailed.
    /// </summary>
    Processed = 5,

    /// <summary>
    /// Card was reported lost by the cardholder.
    /// </summary>
    Lost = 6,

    /// <summary>
    /// Card was reported stolen.
    /// </summary>
    Stolen = 7,

    /// <summary>
    /// Card was reported physically damaged.
    /// </summary>
    Damaged = 8,

    /// <summary>
    /// Card was deactivated by the state agency (not by user action).
    /// </summary>
    DeactivatedByState = 9,

    /// <summary>
    /// Card has been issued but not yet activated by the cardholder.
    /// </summary>
    NotActivated = 10,

    /// <summary>
    /// Card has been temporarily frozen (e.g., suspected fraud).
    /// </summary>
    Frozen = 11,

    /// <summary>
    /// Card was returned as undeliverable by the postal service.
    /// </summary>
    Undeliverable = 12
}
