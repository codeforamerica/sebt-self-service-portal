using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.Api.Composition.Defaults;

/// <summary>
/// Mock implementation of the card replacement plugin for development/testing.
/// Used when UseMockHouseholdData is enabled so that card replacement requests
/// succeed without calling the real state backend.
/// </summary>
internal class MockCardReplacementService : ICardReplacementService
{
    public Task<CardReplacementResult> RequestCardReplacementAsync(
        CardReplacementRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CardReplacementResult.Success());
    }
}
