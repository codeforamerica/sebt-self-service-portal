using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.StatesPlugins.Interfaces;

/// <summary>
/// State plugin interface for initiating card replacement requests against a state's
/// card-issuance system. Each state provides its own implementation that validates
/// policy rules and dispatches the request to the downstream system of record.
/// </summary>
public interface ICardReplacementService : IStatePlugin
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
