using SEBT.Portal.Core.StateBackends;
using IStateAddressUpdateService = SEBT.Portal.StatesPlugins.Interfaces.IAddressUpdateService;
using PluginAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;
using PluginAddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;

namespace SEBT.Portal.Infrastructure.StateBackendAdapters;

/// <summary>
/// Adapts the Core address-update port onto the state-connector plugin contract: the envelope's
/// <c>HouseholdIdentifier</c> drives ONE contract call, an empty <c>CaseIds</c> batch is valid
/// (zero-case households still update), and each present token is cross-checked against the
/// envelope identifier, fail loud.
/// </summary>
public class PluginAddressUpdateBackend(IStateAddressUpdateService addressUpdateService)
    : IAddressUpdateBackend
{
    public async Task<WriteResult> UpdateAddressAsync(
        AddressUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        CrossCheckTokensAgainstEnvelope(request);

        var pluginRequest = new PluginAddressUpdateRequest
        {
            HouseholdIdentifierValue = request.HouseholdIdentifier,
            Address = new PluginAddress
            {
                StreetAddress1 = request.Address.Line1,
                StreetAddress2 = request.Address.Line2,
                City = request.Address.City,
                State = request.Address.State,
                PostalCode = request.Address.Zip,
            },
        };

        var result = await addressUpdateService
            .UpdateAddressAsync(pluginRequest, cancellationToken)
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

    private static void CrossCheckTokensAgainstEnvelope(AddressUpdateRequest request)
    {
        foreach (var token in request.CaseIds)
        {
            var routingFields = OpaqueCaseId.Decode(token);

            if (!routingFields.TryGetValue("householdIdentifier", out string? value))
            {
                throw new InvalidOperationException(
                    "A caseId token is missing the 'householdIdentifier' routing field.");
            }

            if (!string.Equals(request.HouseholdIdentifier, value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A caseId token's 'householdIdentifier' disagrees with the request's household identifier.");
            }
        }
    }
}
