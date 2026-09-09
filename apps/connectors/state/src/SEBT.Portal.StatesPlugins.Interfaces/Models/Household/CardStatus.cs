namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Represents the status of a benefit card. Used only as a plugin-to-portal
/// mapping contract; the corresponding portal-side <c>Core.CardStatus</c>
/// is what gets serialized over the API. Members must stay in sync with
/// the Core enum (parity is enforced by EnumParityTests in the portal).
/// </summary>
public enum CardStatus
{
    Active = 0,
    Damaged = 1,
    DeactivatedByState = 2,
    Frozen = 3,
    Lost = 4,
    NotActivated = 5,
    Processed = 6,
    Stolen = 7,
    Undeliverable = 8,
    Unknown = 9
}
