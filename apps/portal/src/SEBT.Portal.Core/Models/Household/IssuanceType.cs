namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents the type of issuance for an application.
/// </summary>
public enum IssuanceType
{
    /// <summary>
    /// Issuance type is unknown or not set.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Summer EBT (e.g., DC Sun Bucks).
    /// </summary>
    SummerEbt = 1,

    /// <summary>
    /// TANF EBT Card.
    /// </summary>
    TanfEbtCard = 2,

    /// <summary>
    /// SNAP EBT Card.
    /// </summary>
    SnapEbtCard = 3
}
