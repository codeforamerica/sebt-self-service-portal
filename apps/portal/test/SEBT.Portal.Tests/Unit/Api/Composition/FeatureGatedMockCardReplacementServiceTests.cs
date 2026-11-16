using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginCardReplacementReason = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementReason;
using PluginCardReplacementRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementRequest;
using PluginCardReplacementResult = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementResult;
using PluginCaseRef = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CaseRef;

namespace SEBT.Portal.Tests.Unit.Api.Composition;

public class FeatureGatedMockCardReplacementServiceTests
{
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ICardReplacementService _live = Substitute.For<ICardReplacementService>();

    public FeatureGatedMockCardReplacementServiceTests()
    {
        _environment.EnvironmentName.Returns(Environments.Development);
        _live.RequestCardReplacementAsync(Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>())
            .Returns(PluginCardReplacementResult.Success());
    }

    private static PluginCardReplacementRequest Request() =>
        new()
        {
            HouseholdIdentifierValue = "household-1",
            CaseRefs = [new PluginCaseRef { SummerEbtCaseId = "case-1" }],
            Reason = PluginCardReplacementReason.Lost,
        };

    private FeatureGatedMockCardReplacementService Create(bool flag) =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    [$"FeatureManagement:{FeatureFlags.UseMockHouseholdData}"] = flag ? "true" : "false",
                })
                .Build(),
            _environment,
            _logger,
            _live);

    [Fact]
    public async Task RequestCardReplacementAsync_WhenMockFlagOn_SucceedsWithoutCallingLive()
    {
        var sut = Create(flag: true);

        var result = await sut.RequestCardReplacementAsync(Request());

        Assert.True(result.IsSuccess);
        await _live.DidNotReceive().RequestCardReplacementAsync(Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>());
        _logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("served from mock data")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task RequestCardReplacementAsync_WhenStagingAndFlagOn_SucceedsWithoutCallingLive()
    {
        _environment.EnvironmentName.Returns(Environments.Staging);
        var sut = Create(flag: true);

        var result = await sut.RequestCardReplacementAsync(Request());

        Assert.True(result.IsSuccess);
        await _live.DidNotReceive().RequestCardReplacementAsync(Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RequestCardReplacementAsync_WhenProductionAndFlagOn_DelegatesToLiveAndLogsWarning()
    {
        _environment.EnvironmentName.Returns(Environments.Production);
        var sut = Create(flag: true);

        await sut.RequestCardReplacementAsync(Request());

        await _live.Received(1).RequestCardReplacementAsync(Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>());
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
    public async Task RequestCardReplacementAsync_WhenMockFlagOff_DelegatesToLive()
    {
        var sut = Create(flag: false);

        var result = await sut.RequestCardReplacementAsync(Request());

        Assert.True(result.IsSuccess);
        await _live.Received(1).RequestCardReplacementAsync(Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>());
    }
}
