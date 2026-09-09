using SEBT.Portal.Api.Composition.Defaults;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.Tests.Unit.Api.Composition.Defaults;

public class DefaultAddressUpdateServiceTests
{
    [Fact]
    public async Task UpdateAddressAsync_ReturnsNotConfiguredBackendError()
    {
        var service = new DefaultAddressUpdateService();
        var request = new AddressUpdateRequest
        {
            HouseholdIdentifierValue = "guardian@example.com",
            Address = new Address { StreetAddress1 = "42 Test Street", City = "TestCity", State = "DC", PostalCode = "20001" }
        };

        var result = await service.UpdateAddressAsync(request);

        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("NOT_CONFIGURED", result.ErrorCode);
        Assert.Equal("No address update service configured.", result.ErrorMessage);
    }
}
