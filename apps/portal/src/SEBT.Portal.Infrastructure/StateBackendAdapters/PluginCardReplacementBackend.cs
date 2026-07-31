using SEBT.Portal.Core.StateBackends;
using IStateCardReplacementService = SEBT.Portal.StatesPlugins.Interfaces.ICardReplacementService;
using PluginCardReplacementReason = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementReason;
using PluginCardReplacementRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementRequest;
using PluginCaseRef = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CaseRef;

namespace SEBT.Portal.Infrastructure.StateBackendAdapters;

/// <summary>
/// Adapts the Core card-replacement port onto the state-connector plugin contract: decodes each
/// opaque case token into a <see cref="PluginCaseRef"/>, resolves the single household identifier
/// every token must agree on (fail loud on absence or disagreement), and dispatches ONE batched call.
/// </summary>
public class PluginCardReplacementBackend(IStateCardReplacementService cardReplacementService)
    : ICardReplacementBackend
{
    public async Task<WriteResult> RequestCardReplacementAsync(
        CardReplacementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var decodedTokens = request.CaseIds.Select(OpaqueCaseId.Decode).ToList();
        string householdIdentifier = ResolveSharedHouseholdIdentifier(decodedTokens);

        var pluginRequest = new PluginCardReplacementRequest
        {
            HouseholdIdentifierValue = householdIdentifier,
            CaseRefs = decodedTokens.Select(ToCaseRef).ToList(),
            // The portal UI collects no reason; the contract property stays until the contract dies.
            Reason = PluginCardReplacementReason.Unspecified,
        };

        var result = await cardReplacementService
            .RequestCardReplacementAsync(pluginRequest, cancellationToken)
            .ConfigureAwait(false);

        // Field-for-field faithful map — the handler propagates ErrorMessage to the API response.
        return result.IsSuccess
            ? WriteResult.Success()
            : new WriteResult
            {
                IsSuccess = false,
                IsPolicyRejection = result.IsPolicyRejection,
                ErrorCode = result.ErrorCode,
                ErrorMessage = result.ErrorMessage,
            };
    }

    private static PluginCaseRef ToCaseRef(IReadOnlyDictionary<string, string> routingFields)
    {
        if (!routingFields.TryGetValue("caseId", out string? caseId))
        {
            throw new InvalidOperationException(
                "A caseId token is missing the 'caseId' routing field.");
        }

        return new PluginCaseRef
        {
            SummerEbtCaseId = caseId,
            ApplicationId = routingFields.GetValueOrDefault("applicationId"),
            ApplicationStudentId = routingFields.GetValueOrDefault("applicationStudentId"),
        };
    }

    private static string ResolveSharedHouseholdIdentifier(
        IReadOnlyList<IReadOnlyDictionary<string, string>> decodedTokens)
    {
        string? shared = null;
        foreach (var routingFields in decodedTokens)
        {
            if (!routingFields.TryGetValue("householdIdentifier", out string? value))
            {
                throw new InvalidOperationException(
                    "A caseId token is missing the 'householdIdentifier' routing field.");
            }

            if (shared is null)
            {
                shared = value;
            }
            else if (!string.Equals(shared, value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "caseId tokens disagree on 'householdIdentifier' — cannot resolve a single household.");
            }
        }

        return shared
            ?? throw new InvalidOperationException(
                "CaseIds must contain at least one case token.");
    }
}
