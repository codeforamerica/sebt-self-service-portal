using System.Text.Json.Serialization;

namespace SEBT.Portal.Core.Models.Household;

/// <summary>
/// Represents the status of a benefit card. Mirrors the Interface enum
/// member-for-member; parity is enforced by EnumParityTests.
/// Serialized as the member name (e.g., "Active", "Lost") over the API
/// per JsonStringEnumConverter, so member identifiers are the wire contract.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CardStatus
{
    /// <summary>Card is active and can be used for purchases.</summary>
    Active = 0,

    /// <summary>Card was reported physically damaged. Eligible for replacement.</summary>
    Damaged = 1,

    /// <summary>Card was deactivated by the state agency (not by user action).</summary>
    DeactivatedByState = 2,

    /// <summary>Card has been temporarily frozen (e.g., suspected fraud).</summary>
    Frozen = 3,

    /// <summary>Card was reported lost by the cardholder. Eligible for replacement.</summary>
    Lost = 4,

    /// <summary>Card has been issued but not yet activated by the cardholder.</summary>
    NotActivated = 5,

    /// <summary>
    /// Card has been processed and issued. Used as DC's primary "card is on
    /// its way" state, displayed to the user as "Processed on [date]".
    /// </summary>
    Processed = 6,

    /// <summary>Card was reported stolen. Eligible for replacement.</summary>
    Stolen = 7,

    /// <summary>Card was returned as undeliverable by the postal service.</summary>
    Undeliverable = 8,

    /// <summary>
    /// Card status is unknown. Used as the fallback for backend values that
    /// haven't been mapped to one of the named states; the connector should
    /// log an error so the unmapped value can be added to the mapping table.
    /// </summary>
    Unknown = 9
}
