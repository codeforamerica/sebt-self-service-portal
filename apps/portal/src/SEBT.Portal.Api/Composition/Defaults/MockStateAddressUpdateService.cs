using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;
using CoreAddress = SEBT.Portal.Core.Models.Household.Address;

namespace SEBT.Portal.Api.Composition.Defaults;

/// <summary>
/// Mock implementation of the state address update plugin for development/testing.
/// Used when UseMockHouseholdData is enabled so that address updates are persisted
/// to the in-memory mock household store instead of calling the real state backend.
/// </summary>
internal class MockStateAddressUpdateService(MockHouseholdRepository mockHouseholdRepository)
    : IAddressUpdateService
{
    public Task<AddressUpdateResult> UpdateAddressAsync(
        AddressUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        var coreAddress = new CoreAddress
        {
            StreetAddress1 = request.Address.StreetAddress1,
            StreetAddress2 = request.Address.StreetAddress2,
            City = request.Address.City,
            State = request.Address.State,
            PostalCode = request.Address.PostalCode
        };

        mockHouseholdRepository.TryUpdateAddress(request.HouseholdIdentifierValue, coreAddress);

        return Task.FromResult(AddressUpdateResult.Success());
    }
}
