using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginCardReplacementRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementRequest;
using PluginCardReplacementResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementResult;
using PluginCaseRef = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CaseRef;
using CoreCardReplacementRequest = SEBT.Portal.Core.StateBackends.CardReplacementRequest;

namespace SEBT.Portal.Tests.Unit.Api.Composition;

public class FeatureGatedCardReplacementServiceTests
{
    private readonly IFeatureManager _features = Substitute.For<IFeatureManager>();
    private readonly ICardReplacementService _plugin = Substitute.For<ICardReplacementService>();
    private readonly ICardReplacementBackend _adapter = Substitute.For<ICardReplacementBackend>();

    private static PluginCardReplacementRequest PluginRequest() =>
        new()
        {
            HouseholdIdentifierValue = "household-1",
            CaseRefs = [new PluginCaseRef { SummerEbtCaseId = "opaque-token" }],
            Reason = StatesPlugins.Interfaces.Models.Household.CardReplacementReason.Lost,
        };

    [Fact]
    public async Task RequestCardReplacementAsync_WhenFlagOff_DelegatesToPlugin()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(false);
        _plugin.RequestCardReplacementAsync(Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginCardReplacementResult.Success());
        var sut = new FeatureGatedCardReplacementService(_features, _plugin, _adapter);

        var result = await sut.RequestCardReplacementAsync(PluginRequest());

        Assert.True(result.IsSuccess);
        await _plugin.Received(1).RequestCardReplacementAsync(
            Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>());
        await _adapter.DidNotReceive().RequestCardReplacementAsync(
            Arg.Any<CoreCardReplacementRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestCardReplacementAsync_WhenFlagOn_DelegatesToAdapter()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(true);
        _adapter.RequestCardReplacementAsync(Arg.Any<CoreCardReplacementRequest>(), Arg.Any<CancellationToken>())
            .Returns(WriteResult.PolicyRejected("CARD_IN_FLIGHT", "already on the way"));
        var sut = new FeatureGatedCardReplacementService(_features, _plugin, _adapter);

        var result = await sut.RequestCardReplacementAsync(PluginRequest());

        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
        Assert.Equal("CARD_IN_FLIGHT", result.ErrorCode);
        await _adapter.Received(1).RequestCardReplacementAsync(
            Arg.Is<CoreCardReplacementRequest>(r =>
                r.HouseholdIdentifier == "household-1"
                && r.CaseIds.Count == 1
                && r.CaseIds[0] == "opaque-token"),
            Arg.Any<CancellationToken>());
        await _plugin.DidNotReceive().RequestCardReplacementAsync(
            Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestCardReplacementAsync_WhenFlagOnWithoutAdapter_Throws()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(true);
        var sut = new FeatureGatedCardReplacementService(_features, _plugin, adapter: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.RequestCardReplacementAsync(PluginRequest()));
        Assert.Contains("ConfigPath", ex.Message);
    }
}
