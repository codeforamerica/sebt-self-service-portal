using SEBT.Portal.Core.StateConnector;
using IPluginAddressUpdateService = SEBT.Portal.StatesPlugins.Interfaces.IAddressUpdateService;
using PluginAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;
using PluginAddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;

namespace SEBT.Portal.Infrastructure.StateConnector;

/// <summary>
/// Adapter that fulfills the Core <see cref="IStateAddressUpdateService"/> port by
/// delegating to the loaded state plugin's address update service. Maps Core models
/// to plugin contract models (and back) at the boundary.
/// </summary>
public class PluginAddressUpdateService(
    IPluginAddressUpdateService pluginAddressUpdateService) : IStateAddressUpdateService
{
    /// <inheritdoc />
    public async Task<AddressUpdateResult> UpdateAddressAsync(
        AddressUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var pluginRequest = new PluginAddressUpdateRequest
        {
            HouseholdIdentifierValue = request.HouseholdIdentifierValue,
            Address = new PluginAddress
            {
                StreetAddress1 = request.Address.StreetAddress1,
                StreetAddress2 = request.Address.StreetAddress2,
                City = request.Address.City,
                State = request.Address.State,
                PostalCode = request.Address.PostalCode
            }
        };

        var pluginResult = await pluginAddressUpdateService.UpdateAddressAsync(
            pluginRequest, cancellationToken);

        return new AddressUpdateResult
        {
            IsSuccess = pluginResult.IsSuccess,
            IsPolicyRejection = pluginResult.IsPolicyRejection,
            ErrorCode = pluginResult.ErrorCode,
            ErrorMessage = pluginResult.ErrorMessage
        };
    }
}
