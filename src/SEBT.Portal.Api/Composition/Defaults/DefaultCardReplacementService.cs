using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.Api.Composition.Defaults;

/// <summary>
/// Default implementation when no state-specific ICardReplacementService plugin is loaded.
/// Returns a backend error indicating the service is not configured.
/// </summary>
internal class DefaultCardReplacementService : ICardReplacementService
{
    public Task<CardReplacementResult> RequestCardReplacementAsync(
        CardReplacementRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            CardReplacementResult.BackendError("NOT_CONFIGURED", "No card replacement service configured."));
    }
}
