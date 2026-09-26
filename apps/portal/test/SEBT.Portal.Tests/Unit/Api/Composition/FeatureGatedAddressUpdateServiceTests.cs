using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;
using PluginAddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;
using PluginAddressUpdateResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateResult;

namespace SEBT.Portal.Tests.Unit.Api.Composition;

public class FeatureGatedAddressUpdateServiceTests
{
    private readonly IFeatureManager _features = Substitute.For<IFeatureManager>();
    private readonly IAddressUpdateService _plugin = Substitute.For<IAddressUpdateService>();
    private readonly IAddressUpdateBackend _adapter = Substitute.For<IAddressUpdateBackend>();

    private static PluginAddressUpdateRequest PluginRequest() =>
        new()
        {
            HouseholdIdentifierValue = "household-1",
            CaseIds = ["case-token-1", "case-token-2"],
            Address = new PluginAddress
            {
                StreetAddress1 = "1 Main St",
                City = "Denver",
                State = "CO",
                PostalCode = "80202",
            },
        };

    [Fact]
    public async Task UpdateAddressAsync_WhenFlagOff_DelegatesToPlugin()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(false);
        _plugin.UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.Success());
        var sut = new FeatureGatedAddressUpdateService(_features, _plugin, _adapter);

        var result = await sut.UpdateAddressAsync(PluginRequest());

        Assert.True(result.IsSuccess);
        await _plugin.Received(1).UpdateAddressAsync(
            Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
        await _adapter.DidNotReceive().UpdateAddressAsync(
            Arg.Any<AddressUpdateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAddressAsync_WhenFlagOn_DelegatesToAdapter()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(true);
        _adapter.UpdateAddressAsync(Arg.Any<AddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(WriteResult.Success());
        var sut = new FeatureGatedAddressUpdateService(_features, _plugin, _adapter);

        var result = await sut.UpdateAddressAsync(PluginRequest());

        Assert.True(result.IsSuccess);
        await _adapter.Received(1).UpdateAddressAsync(
            Arg.Is<AddressUpdateRequest>(r =>
                r.HouseholdIdentifier == "household-1"
                && r.Address.Line1 == "1 Main St"
                && r.Address.Zip == "80202"
                && r.CaseIds.SequenceEqual(new[] { "case-token-1", "case-token-2" })),
            Arg.Any<CancellationToken>());
    }
}
