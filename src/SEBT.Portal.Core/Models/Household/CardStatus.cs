using System.Text.Json.Serialization;

namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents the status of a benefit card. Mirrors the Interface enum
/// member-for-member; parity is enforced by EnumParityTests.
/// Serialized as the member name over the API per JsonStringEnumConverter.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
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
