using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;
using NSubstitute;
using SEBT.Portal.Api.Composition;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;
using PluginAddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;
using PluginCardReplacementReason = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementReason;
using PluginCardReplacementRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementRequest;
using PluginCaseRef = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CaseRef;

namespace SEBT.Portal.Tests.Unit.Api.Composition;

public class MockHouseholdRoutingTests
{
    [Fact]
    public async Task ReadsAndWrites_WhenMockFlagFlipped_AgreeOnBackend()
    {
        var configuration = new ConfigurationManager();
        configuration[$"FeatureManagement:{FeatureFlags.UseMockHouseholdData}"] = "true";
        var liveAddress = Substitute.For<IAddressUpdateService>();
        var liveCardReplacement = Substitute.For<ICardReplacementService>();
        await using var provider = BuildProvider(configuration, liveAddress, liveCardReplacement);

        Assert.IsType<MockHouseholdRepository>(provider.GetRequiredService<IHouseholdRepository>());
        await provider.GetRequiredService<IAddressUpdateService>().UpdateAddressAsync(Request());
        await liveAddress.DidNotReceive().UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
        await provider.GetRequiredService<ICardReplacementService>().RequestCardReplacementAsync(CardReplacementRequest());
        await liveCardReplacement.DidNotReceive().RequestCardReplacementAsync(
            Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>());

        configuration[$"FeatureManagement:{FeatureFlags.UseMockHouseholdData}"] = "false";

        Assert.IsType<HouseholdRepository>(provider.GetRequiredService<IHouseholdRepository>());
        await provider.GetRequiredService<IAddressUpdateService>().UpdateAddressAsync(Request());
        await liveAddress.Received(1).UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
        await provider.GetRequiredService<ICardReplacementService>().RequestCardReplacementAsync(CardReplacementRequest());
        await liveCardReplacement.Received(1).RequestCardReplacementAsync(
            Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadsAndWrites_WhenFlagUsesFeatureFilter_AgreeOnLiveBackend()
    {
        var configuration = new ConfigurationManager();
        var flag = $"FeatureManagement:{FeatureFlags.UseMockHouseholdData}";
        configuration[$"{flag}:EnabledFor:0:Name"] = "Microsoft.Percentage";
        configuration[$"{flag}:EnabledFor:0:Parameters:Value"] = "100";
        var liveAddress = Substitute.For<IAddressUpdateService>();
        var liveCardReplacement = Substitute.For<ICardReplacementService>();
        await using var provider = BuildProvider(configuration, liveAddress, liveCardReplacement, percentageFilter: true);

        Assert.True(await provider.GetRequiredService<IFeatureManager>()
            .IsEnabledAsync(FeatureFlags.UseMockHouseholdData));
        Assert.IsType<HouseholdRepository>(provider.GetRequiredService<IHouseholdRepository>());
        await provider.GetRequiredService<IAddressUpdateService>().UpdateAddressAsync(Request());
        await liveAddress.Received(1).UpdateAddressAsync(Arg.Any<PluginAddressUpdateRequest>(), Arg.Any<CancellationToken>());
        await provider.GetRequiredService<ICardReplacementService>().RequestCardReplacementAsync(CardReplacementRequest());
        await liveCardReplacement.Received(1).RequestCardReplacementAsync(
            Arg.Any<PluginCardReplacementRequest>(), Arg.Any<CancellationToken>());
    }

    private static ServiceProvider BuildProvider(
        ConfigurationManager configuration,
        IAddressUpdateService liveAddress,
        ICardReplacementService liveCardReplacement,
        bool percentageFilter = false)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(Environments.Development);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(environment);
        var features = services.AddFeatureManagement(configuration.GetSection("FeatureManagement"));
        if (percentageFilter)
        {
            features.AddFeatureFilter<PercentageFilter>();
        }

        services.AddKeyedSingleton<IAddressUpdateService>(StatePluginKeys.Connector, liveAddress);
        services.AddKeyedSingleton(StatePluginKeys.Connector, liveCardReplacement);
        services.AddKeyedSingleton<ISummerEbtCaseService>(
            StatePluginKeys.Connector,
            Substitute.For<ISummerEbtCaseService>());
        services.AddRepositories();
        services.AddFeatureGatedStateBackendRouting();
        return services.BuildServiceProvider();
    }

    private static PluginAddressUpdateRequest Request() =>
        new()
        {
            HouseholdIdentifierValue = "household-1",
            Address = new PluginAddress
            {
                StreetAddress1 = "1 Main St",
                City = "Denver",
                State = "CO",
                PostalCode = "80202",
            },
        };

    private static PluginCardReplacementRequest CardReplacementRequest() =>
        new()
        {
            HouseholdIdentifierValue = "household-1",
            CaseRefs = [new PluginCaseRef { SummerEbtCaseId = "case-1" }],
            Reason = PluginCardReplacementReason.Lost,
        };
}
