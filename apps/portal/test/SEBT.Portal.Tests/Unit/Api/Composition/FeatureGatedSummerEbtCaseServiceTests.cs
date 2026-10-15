using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginHouseholdIdentifierType = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdIdentifierType;
using PluginIdentityAssuranceLevel = SEBT.Portal.StatesPlugins.Interfaces.Models.IdentityAssuranceLevel;
using PluginPiiVisibility = SEBT.Portal.StatesPlugins.Interfaces.Models.PiiVisibility;
using PluginHouseholdData = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.HouseholdData;

namespace SEBT.Portal.Tests.Unit.Api.Composition;

public class FeatureGatedSummerEbtCaseServiceTests
{
    private static readonly PluginPiiVisibility FullPii = new(IncludeAddress: true, IncludeEmail: true, IncludePhone: true);

    private readonly IFeatureManager _features = Substitute.For<IFeatureManager>();
    private readonly ISummerEbtCaseService _plugin = Substitute.For<ISummerEbtCaseService>();
    private readonly IHouseholdLookupBackend _adapter = Substitute.For<IHouseholdLookupBackend>();

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenFlagOff_DelegatesToPlugin()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(false);
        _plugin.GetHouseholdByIdentifierAsync(
                Arg.Any<PluginHouseholdIdentifierType>(),
                Arg.Any<string>(),
                Arg.Any<PluginPiiVisibility>(),
                Arg.Any<PluginIdentityAssuranceLevel>(),
                Arg.Any<Guid?>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(new PluginHouseholdData { Phone = "5551112222" });
        var sut = new FeatureGatedSummerEbtCaseService(_features, _plugin, _adapter);

        var result = await sut.GetHouseholdByIdentifierAsync(
            PluginHouseholdIdentifierType.Phone,
            "5551112222",
            FullPii,
            PluginIdentityAssuranceLevel.IAL1);

        Assert.NotNull(result);
        Assert.Equal("5551112222", result.Phone);
        await _plugin.Received(1).GetHouseholdByIdentifierAsync(
            PluginHouseholdIdentifierType.Phone,
            "5551112222",
            FullPii,
            PluginIdentityAssuranceLevel.IAL1,
            Arg.Any<Guid?>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
        await _adapter.DidNotReceive().LookupHouseholdAsync(
            Arg.Any<HouseholdLookupRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenFlagOn_UsesLookupBackend()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(true);
        _adapter.LookupHouseholdAsync(Arg.Any<HouseholdLookupRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HouseholdLookupResult(
                HouseholdLookupStatus.Found,
                new HouseholdData { Phone = "5551112222" }));
        var sut = new FeatureGatedSummerEbtCaseService(_features, _plugin, _adapter);

        var result = await sut.GetHouseholdByIdentifierAsync(
            PluginHouseholdIdentifierType.Phone,
            "5551112222",
            FullPii,
            PluginIdentityAssuranceLevel.IAL1);

        Assert.NotNull(result);
        Assert.Equal("5551112222", result.Phone);
        await _adapter.Received(1).LookupHouseholdAsync(
            Arg.Is<HouseholdLookupRequest>(r =>
                r.Signals.Count == 1
                && r.Signals[0].Type == "phone"
                && r.Signals[0].Value == "5551112222"
                && r.IsProofed
                && r.HouseholdIdentifier == "5551112222"),
            Arg.Any<CancellationToken>());
        await _plugin.DidNotReceive().GetHouseholdByIdentifierAsync(
            Arg.Any<PluginHouseholdIdentifierType>(),
            Arg.Any<string>(),
            Arg.Any<PluginPiiVisibility>(),
            Arg.Any<PluginIdentityAssuranceLevel>(),
            Arg.Any<Guid?>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetHouseholdByIdentifierAsync_WhenFlagOnWithoutAdapter_Throws()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(true);
        var sut = new FeatureGatedSummerEbtCaseService(_features, _plugin, adapter: null);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetHouseholdByIdentifierAsync(
                PluginHouseholdIdentifierType.Phone,
                "5551112222",
                FullPii,
                PluginIdentityAssuranceLevel.IAL1));
        Assert.Contains("ConfigPath", ex.Message);
    }

    [Fact]
    public async Task TryMatchCoLoadedGuardianByBenefitIdAndDobAsync_WhenFlagOn_UsesLookupBackend()
    {
        _features.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend).Returns(true);
        _adapter.LookupHouseholdAsync(Arg.Any<HouseholdLookupRequest>(), Arg.Any<CancellationToken>())
            .Returns(new HouseholdLookupResult(HouseholdLookupStatus.Found, new HouseholdData()));
        var sut = new FeatureGatedSummerEbtCaseService(_features, _plugin, _adapter);
        var portalUserId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");

        var matched = await sut.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
            "IC-99",
            new DateOnly(1980, 1, 2),
            portalUserId);

        Assert.True(matched);
        await _adapter.Received(1).LookupHouseholdAsync(
            Arg.Is<HouseholdLookupRequest>(r =>
                r.Signals.Any(s => s.Type == "ic" && s.Value == "IC-99")
                && r.Signals.Any(s => s.Type == "dob" && s.Value == "1980-01-02")
                && r.PortalUuid == portalUserId.ToString()),
            Arg.Any<CancellationToken>());
    }
}
