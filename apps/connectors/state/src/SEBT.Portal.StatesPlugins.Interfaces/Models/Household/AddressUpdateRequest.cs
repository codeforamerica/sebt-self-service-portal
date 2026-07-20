namespace SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

/// <summary>
/// Request to update a household's mailing address via a state connector.
/// </summary>
public class AddressUpdateRequest
{
    /// <summary>The household identifier value (e.g., guardian email) resolved by the portal.</summary>
    public required string HouseholdIdentifierValue { get; init; }

    /// <summary>The validated mailing address to persist.</summary>
    public required Address Address { get; init; }
}
