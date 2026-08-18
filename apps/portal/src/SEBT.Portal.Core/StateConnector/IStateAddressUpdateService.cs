namespace SEBT.Portal.Core.StateConnector;

/// <summary>
/// Portal port for persisting mailing address updates to a state's backend systems
/// through the loaded state connector plugin. Adapters in Infrastructure map between
/// these Core models and the plugin contract at the boundary.
/// </summary>
public interface IStateAddressUpdateService
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
