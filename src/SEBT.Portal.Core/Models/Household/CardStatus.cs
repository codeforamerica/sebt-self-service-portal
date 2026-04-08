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
    /// Card status is unknown or could not be determined.
    /// </summary>
    Unknown = 4,

    /// <summary>
    /// Card has been processed by the card issuer.
    /// </summary>
    Processed = 5,

    /// <summary>
    /// Card has been reported as lost.
    /// </summary>
    Lost = 6,

    /// <summary>
    /// Card has been reported as stolen.
    /// </summary>
    Stolen = 7,

    /// <summary>
    /// Card has been reported as damaged.
    /// </summary>
    Damaged = 8,

    /// <summary>
    /// Card has been deactivated by the state agency.
    /// </summary>
    DeactivatedByState = 9,

    /// <summary>
    /// Card has been issued but never activated by the recipient.
    /// </summary>
    NotActivated = 10,

    /// <summary>
    /// Card has been temporarily frozen and cannot be used.
    /// </summary>
    Frozen = 11,

    /// <summary>
    /// Card was returned as undeliverable by the postal service.
    /// </summary>
    Undeliverable = 12
}
