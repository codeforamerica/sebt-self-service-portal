using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;
using PluginAddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;
using PluginAddressUpdateResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateResult;

namespace SEBT.Portal.Tests.Unit.Api.Composition;

public class FeatureGatedMockAddressUpdateServiceTests
{
    private readonly IFeatureManager _features = Substitute.For<IFeatureManager>();
    private readonly IAddressUpdateService _mock = Substitute.For<IAddressUpdateService>();
    private readonly IAddressUpdateService _live = Substitute.For<IAddressUpdateService>();

    private static PluginAddressUpdateRequest Request() =>
        new()
        {
            HouseholdIdentifierValue = "household-1",
            Address = new PluginAddress { StreetAddress1 = "1 Main St", City = "Denver", State = "CO", PostalCode = "80202" },
        };

    [Fact]
    public async Task UpdateAddressAsync_WhenMockFlagOn_DelegatesToMock()
    {
        _features.IsEnabledAsync(FeatureFlags.UseMockHouseholdData).Returns(true);
        _mock.UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.Success());
        var sut = new FeatureGatedMockAddressUpdateService(_features, _mock, _live);

        var result = await sut.UpdateAddressAsync(Request());

        Assert.True(result.IsSuccess);
        await _mock.Received(1).UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
        await _live.DidNotReceive().UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAddressAsync_WhenMockFlagOff_DelegatesToLive()
    {
        _features.IsEnabledAsync(FeatureFlags.UseMockHouseholdData).Returns(false);
        _live.UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.Success());
        var sut = new FeatureGatedMockAddressUpdateService(_features, _mock, _live);

        var result = await sut.UpdateAddressAsync(Request());

        Assert.True(result.IsSuccess);
        await _live.Received(1).UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
        await _mock.DidNotReceive().UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
    }
}
