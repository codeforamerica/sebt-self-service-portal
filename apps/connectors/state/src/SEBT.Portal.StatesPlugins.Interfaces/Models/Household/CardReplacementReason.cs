namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Reason a household is requesting a replacement card.
/// Kept separate from <see cref="CardStatus"/> because current card state and
/// replacement intent are different concepts (e.g., a card can be Active while
/// the user requests a replacement because they just lost it).
/// </summary>
public enum CardReplacementReason
{
    /// <summary>No reason captured. Used when the portal UI does not collect a reason.</summary>
    Unspecified = 0,
    Lost = 1,
    Stolen = 2,
    Damaged = 3,
    Undeliverable = 4,
}
