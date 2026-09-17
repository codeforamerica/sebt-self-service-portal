using Microsoft.FeatureManagement;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginCardReplacementRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementRequest;
using PluginCardReplacementResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementResult;
using CoreCardReplacementRequest = SEBT.Portal.Core.StateBackends.CardReplacementRequest;

namespace SEBT.Portal.Api.Composition;

internal sealed class FeatureGatedCardReplacementService(
    IFeatureManager featureManager,
    ICardReplacementService plugin,
    ICardReplacementBackend? adapter) : ICardReplacementService
{
    public async Task<PluginCardReplacementResult> RequestCardReplacementAsync(
        PluginCardReplacementRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await ConfigurableStateBackendGate.UseAdapterAsync(featureManager, adapter))
        {
            return await plugin.RequestCardReplacementAsync(request, cancellationToken);
        }

        var caseIds = request.CaseRefs
            .Select(r => r.SummerEbtCaseId)
            .ToList();
        var coreRequest = new CoreCardReplacementRequest(caseIds)
        {
            HouseholdIdentifier = request.HouseholdIdentifierValue,
        };

        WriteResult result = await adapter!.RequestCardReplacementAsync(coreRequest, cancellationToken);
        return FeatureGatedWriteResultMapper.Map(
            result,
            PluginCardReplacementResult.Success,
            PluginCardReplacementResult.PolicyRejected,
            PluginCardReplacementResult.BackendError);
    }
}
