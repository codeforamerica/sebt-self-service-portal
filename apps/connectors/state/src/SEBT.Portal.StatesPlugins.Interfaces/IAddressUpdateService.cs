using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatesPlugins.Interfaces;

/// <summary>
/// State plugin interface for persisting mailing address updates to a state's backend systems.
/// Each state provides its own implementation that validates policy rules and writes to
/// the state's authoritative system of record.
/// </summary>
public interface IAddressUpdateService : IStatePlugin
{
    /// <summary>
    /// Persists a mailing address update for the identified household.
    /// </summary>
    /// <param name="request">The address update request containing household identifier and new address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success, policy rejection, or backend error.</returns>
    Task<AddressUpdateResult> UpdateAddressAsync(
        AddressUpdateRequest request,
        CancellationToken cancellationToken = default);
}
