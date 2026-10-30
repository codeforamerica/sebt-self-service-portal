using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly IAddressUpdateService _mock = Substitute.For<IAddressUpdateService>();
    private readonly IAddressUpdateService _live = Substitute.For<IAddressUpdateService>();

    public FeatureGatedMockAddressUpdateServiceTests()
    {
        _environment.EnvironmentName.Returns(Environments.Development);
    }

    private static PluginAddressUpdateRequest Request() =>
        new()
        {
            HouseholdIdentifierValue = "household-1",
            Address = new PluginAddress { StreetAddress1 = "1 Main St", City = "Denver", State = "CO", PostalCode = "80202" },
        };

    private (FeatureGatedMockAddressUpdateService Sut, Lazy<IAddressUpdateService> Mock) Create(bool flag)
    {
        var mock = new Lazy<IAddressUpdateService>(() => _mock);
        var sut = new FeatureGatedMockAddressUpdateService(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"FeatureManagement:{FeatureFlags.UseMockHouseholdData}"] = flag ? "true" : "false",
                })
                .Build(),
            _environment,
            _logger,
            mock,
            _live);
        return (sut, mock);
    }

    [Fact]
    public async Task UpdateAddressAsync_WhenMockFlagOn_DelegatesToMock()
    {
        _mock.UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.Success());
        var (sut, mock) = Create(flag: true);

        var result = await sut.UpdateAddressAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.True(mock.IsValueCreated);
        await _mock.Received(1).UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
        await _live.DidNotReceive().UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("served from mock data")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task UpdateAddressAsync_WhenStagingAndFlagOn_DelegatesToMock()
    {
        _environment.EnvironmentName.Returns(Environments.Staging);
        _mock.UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.Success());
        var (sut, mock) = Create(flag: true);

        await sut.UpdateAddressAsync(Request());

        Assert.True(mock.IsValueCreated);
        await _mock.Received(1).UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
        await _live.DidNotReceive().UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAddressAsync_WhenProductionAndFlagOn_DelegatesToLiveAndLogsWarning()
    {
        _environment.EnvironmentName.Returns(Environments.Production);
        _live.UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.Success());
        var (sut, mock) = Create(flag: true);

        await sut.UpdateAddressAsync(Request());

        Assert.False(mock.IsValueCreated);
        await _live.Received(1).UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
        await _mock.DidNotReceive().UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(FeatureFlags.UseMockHouseholdData)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
        _logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("served from mock data")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task UpdateAddressAsync_WhenMockFlagOff_DelegatesToLive()
    {
        _live.UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginAddressUpdateResult.Success());
        var (sut, mock) = Create(flag: false);

        var result = await sut.UpdateAddressAsync(Request());

        Assert.True(result.IsSuccess);
        Assert.False(mock.IsValueCreated);
        await _live.Received(1).UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
        await _mock.DidNotReceive().UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
    }
}
