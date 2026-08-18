namespace SEBT.Portal.Core.StateConnector;

/// <summary>
/// Portal port for initiating card replacement requests against a state's
/// card-issuance system through the loaded state connector plugin. Adapters in
/// Infrastructure map between these Core models and the plugin contract at the boundary.
/// </summary>
public interface IStateCardReplacementService
{
    /// <summary>
    /// Initiates a replacement card request for one or more cases in the identified household.
    /// Implementations are expected to update any cooldown timestamp the state tracks so
    /// subsequent portal reads reflect the new request time.
    /// </summary>
    /// <param name="request">Household identifier, case IDs, and replacement reason.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success, policy rejection, or backend error.</returns>
    Task<CardReplacementResult> RequestCardReplacementAsync(
        CardReplacementRequest request,
        CancellationToken cancellationToken = default);
}
