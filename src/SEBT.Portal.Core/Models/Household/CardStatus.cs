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
    Deactivated = 3
}
