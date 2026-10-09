namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Request to update a household's mailing address via a state connector.
/// </summary>
public class AddressUpdateRequest
{
    /// <summary>The household identifier value (e.g., guardian email) resolved by the portal.</summary>
    public required string HouseholdIdentifierValue { get; init; }

    /// <summary>
    /// Case identifiers for every case on the household, as returned by the household read.
    /// Connectors that resolve their own write targets from the household identifier may ignore this.
    /// </summary>
    public IReadOnlyList<string> CaseIds { get; init; } = [];

    /// <summary>The validated mailing address to persist.</summary>
    public required Address Address { get; init; }
}
