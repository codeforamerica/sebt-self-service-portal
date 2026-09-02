using SEBT.Portal.Core.StateConnector;
using IPluginCardReplacementService = SEBT.Portal.StatesPlugins.Interfaces.ICardReplacementService;
using PluginCardReplacementReason = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementReason;
using PluginCardReplacementRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementRequest;
using PluginCaseRef = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CaseRef;

namespace SEBT.Portal.Infrastructure.StateConnector;

/// <summary>
/// Adapter that fulfills the Core <see cref="IStateCardReplacementService"/> port by
/// delegating to the loaded state plugin's card replacement service. Maps Core models
/// to plugin contract models (and back) at the boundary.
/// </summary>
public class PluginCardReplacementService(
    IPluginCardReplacementService pluginCardReplacementService) : IStateCardReplacementService
{
    /// <inheritdoc />
    public async Task<CardReplacementResult> RequestCardReplacementAsync(
        CardReplacementRequest request,
        CancellationToken cancellationToken = default)
    {
        var pluginRequest = new PluginCardReplacementRequest
        {
            HouseholdIdentifierValue = request.HouseholdIdentifierValue,
            CaseRefs = request.CaseRefs
                .Select(caseRef => new PluginCaseRef
                {
                    SummerEbtCaseId = caseRef.SummerEbtCaseId,
                    ApplicationId = caseRef.ApplicationId,
                    ApplicationStudentId = caseRef.ApplicationStudentId
                })
                .ToList(),
            Reason = MapReason(request.Reason)
        };

        var pluginResult = await pluginCardReplacementService.RequestCardReplacementAsync(
            pluginRequest, cancellationToken);

        return new CardReplacementResult
        {
            IsSuccess = pluginResult.IsSuccess,
            IsPolicyRejection = pluginResult.IsPolicyRejection,
            ErrorCode = pluginResult.ErrorCode,
            ErrorMessage = pluginResult.ErrorMessage
        };
    }

    // Explicit switch (not a cast) so a contract enum change fails loudly at compile
    // or run time instead of silently mapping to the wrong plugin value.
    private static PluginCardReplacementReason MapReason(CardReplacementReason reason) =>
        reason switch
        {
            CardReplacementReason.Unspecified => PluginCardReplacementReason.Unspecified,
            CardReplacementReason.Lost => PluginCardReplacementReason.Lost,
            CardReplacementReason.Stolen => PluginCardReplacementReason.Stolen,
            CardReplacementReason.Damaged => PluginCardReplacementReason.Damaged,
            CardReplacementReason.Undeliverable => PluginCardReplacementReason.Undeliverable,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown card replacement reason.")
        };
}
