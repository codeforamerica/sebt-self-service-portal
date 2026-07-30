using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Infrastructure;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.StateBackendAdapters;
using SEBT.Portal.Infrastructure.StateBackends;
using SEBT.Portal.Tests.Unit.Infrastructure.StateBackends.ConfigSamples;
using IStateAddressUpdateService = SEBT.Portal.StatesPlugins.Interfaces.IAddressUpdateService;
using IStateCardReplacementService = SEBT.Portal.StatesPlugins.Interfaces.ICardReplacementService;
using IStateEnrollmentCheckService = SEBT.Portal.StatesPlugins.Interfaces.IEnrollmentCheckService;
using ISummerEbtCaseService = SEBT.Portal.StatesPlugins.Interfaces.ISummerEbtCaseService;

namespace SEBT.Portal.Tests.Unit.Infrastructure;

/// <summary>
/// Tests for the state-backend port registrations in <see cref="Dependencies"/>: the
/// use_configurable_state_backend flag switches the three write/enrollment ports and the
/// household read path together between the plugin path (flag off — today's behavior) and
/// the config-driven backend (flag on). Mock household data beats the flag.
/// </summary>
public sealed class StateBackendPortRegistrationTests : IDisposable
{
    private const string FlagKey =
        $"FeatureManagement:{FeatureFlags.UseConfigurableStateBackend}";
    private const string ConfigPathKey = "StateBackend:ConfigPath";

    private readonly string _tempDir =
        Directory.CreateTempSubdirectory("state-backend-port-tests-").FullName;

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string WriteConfigFile(string yaml)
    {
        var path = Path.Combine(_tempDir, "state-backend.yaml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private static ServiceProvider BuildProvider(Dictionary<string, string?> settings)
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddPortalInfrastructureServices(configuration);
        services.AddPortalInfrastructureRepositories(configuration);

        // The plugin adapters consume the contract services AddPlugins registers in production.
        services.AddSingleton(Substitute.For<IStateCardReplacementService>());
        services.AddSingleton(Substitute.For<IStateAddressUpdateService>());
        services.AddSingleton(Substitute.For<IStateEnrollmentCheckService>());
        services.AddSingleton(Substitute.For<ISummerEbtCaseService>());

        return services.BuildServiceProvider();
    }

    // ---------------------------------------------------------------------------
    // Flag OFF (default) — plugin adapters, config-driven stack never constructed
    // ---------------------------------------------------------------------------

    [Fact]
    public void FlagOff_PortsResolveToPluginAdapters()
    {
        using var provider = BuildProvider([]);

        Assert.IsType<PluginCardReplacementBackend>(
            provider.GetRequiredService<ICardReplacementBackend>());
        Assert.IsType<PluginAddressUpdateBackend>(
            provider.GetRequiredService<IAddressUpdateBackend>());
        Assert.IsType<PluginEnrollmentCheckBackend>(
            provider.GetRequiredService<IEnrollmentCheckBackend>());
    }

    [Fact]
    public void FlagOff_NeverTouchesConfigPath()
    {
        // A ConfigPath pointing at a nonexistent file would fail loud if the config-driven
        // stack were ever constructed (the flag-on tests below prove it does). Resolving
        // cleanly here proves the flag-off path never reads it — the stack stays lazy.
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [FlagKey] = "false",
            [ConfigPathKey] = Path.Combine(Path.GetTempPath(), "does-not-exist.yaml"),
        });

        Assert.IsType<PluginCardReplacementBackend>(
            provider.GetRequiredService<ICardReplacementBackend>());
        Assert.IsType<PluginAddressUpdateBackend>(
            provider.GetRequiredService<IAddressUpdateBackend>());
        Assert.IsType<PluginEnrollmentCheckBackend>(
            provider.GetRequiredService<IEnrollmentCheckBackend>());
    }

    // ---------------------------------------------------------------------------
    // Flag ON + valid YAML — one shared ConfigurableStateBackend behind all ports
    // ---------------------------------------------------------------------------

    [Fact]
    public void FlagOn_WithValidConfig_PortsResolveToSharedConfigurableStateBackend()
    {
        var configPath = WriteConfigFile(SampleLoader.Load("dc.sample.yaml"));
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [FlagKey] = "true",
            [ConfigPathKey] = configPath,
        });

        var cardReplacement = provider.GetRequiredService<ICardReplacementBackend>();
        var addressUpdate = provider.GetRequiredService<IAddressUpdateBackend>();
        var enrollmentCheck = provider.GetRequiredService<IEnrollmentCheckBackend>();

        Assert.IsType<ConfigurableStateBackend>(cardReplacement);
        Assert.Same(cardReplacement, addressUpdate);
        Assert.Same(cardReplacement, enrollmentCheck);
    }

    // ---------------------------------------------------------------------------
    // Flag ON + missing/invalid config — loud failure
    // ---------------------------------------------------------------------------

    [Fact]
    public void FlagOn_WithoutConfigPath_FailsLoud()
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [FlagKey] = "true",
        });

        var ex = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<ICardReplacementBackend>());

        Assert.Contains(ConfigPathKey, ex.Message);
    }

    [Fact]
    public void FlagOn_WithMissingConfigFile_FailsLoud()
    {
        var missingPath = Path.Combine(_tempDir, "does-not-exist.yaml");
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [FlagKey] = "true",
            [ConfigPathKey] = missingPath,
        });

        var ex = Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<ICardReplacementBackend>());

        Assert.Contains(missingPath, ex.Message);
    }

    // ---------------------------------------------------------------------------
    // Flag ON + YAML missing an operation — throwing null-object for that port only
    // ---------------------------------------------------------------------------

    private const string CardReplacementOnlyYaml =
        """
        baseUrl: http://backend.test
        auth:
          scheme: api_key
          header: X-Api-Key
          keyRef: test-api-key
        operations:
          cardReplacement:
            method: post
            path: /cards/replace
        """;

    [Fact]
    public void FlagOn_WithUndeclaredOperations_DeclaredPortStillResolvesToBackend()
    {
        var configPath = WriteConfigFile(CardReplacementOnlyYaml);
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [FlagKey] = "true",
            [ConfigPathKey] = configPath,
        });

        Assert.IsType<ConfigurableStateBackend>(
            provider.GetRequiredService<ICardReplacementBackend>());
    }

    [Fact]
    public async Task FlagOn_WithUndeclaredAddressUpdate_PortThrowsNotSupported()
    {
        var configPath = WriteConfigFile(CardReplacementOnlyYaml);
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [FlagKey] = "true",
            [ConfigPathKey] = configPath,
        });

        var port = provider.GetRequiredService<IAddressUpdateBackend>();
        Assert.IsType<UnsupportedStateBackendOperation>(port);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => port.UpdateAddressAsync(
            new AddressUpdateRequest("household-1", [], new AddressUpdateAddress())));

        Assert.Contains("Address update", ex.Message);
        Assert.Contains("backend configuration", ex.Message);
    }

    [Fact]
    public async Task FlagOn_WithUndeclaredEnrollmentCheck_PortThrowsNotSupported()
    {
        var configPath = WriteConfigFile(CardReplacementOnlyYaml);
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [FlagKey] = "true",
            [ConfigPathKey] = configPath,
        });

        var port = provider.GetRequiredService<IEnrollmentCheckBackend>();
        Assert.IsType<UnsupportedStateBackendOperation>(port);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => port.CheckEnrollmentAsync(
            new EnrollmentCheckRequest([])));

        Assert.Contains("Enrollment check", ex.Message);
        Assert.Contains("backend configuration", ex.Message);
    }

    [Fact]
    public async Task FlagOn_WithUndeclaredCardReplacement_PortThrowsNotSupported()
    {
        const string addressUpdateOnlyYaml =
            """
            baseUrl: http://backend.test
            auth:
              scheme: api_key
              header: X-Api-Key
              keyRef: test-api-key
            operations:
              addressUpdate:
                method: post
                path: /households/address
            """;
        var configPath = WriteConfigFile(addressUpdateOnlyYaml);
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [FlagKey] = "true",
            [ConfigPathKey] = configPath,
        });

        var port = provider.GetRequiredService<ICardReplacementBackend>();
        Assert.IsType<UnsupportedStateBackendOperation>(port);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => port.RequestCardReplacementAsync(new CardReplacementRequest(["case-token"])));

        Assert.Contains("Card replacement", ex.Message);
        Assert.Contains("backend configuration", ex.Message);
    }

    // ---------------------------------------------------------------------------
    // Household read path — same atomic flag; mock data beats it
    // ---------------------------------------------------------------------------

    [Fact]
    public void FlagOff_HouseholdRepositoryResolvesToPluginRepository()
    {
        using var provider = BuildProvider([]);

        Assert.IsType<HouseholdRepository>(
            provider.GetRequiredService<IHouseholdRepository>());
    }

    [Fact]
    public void FlagOn_HouseholdRepositoryResolvesToStateBackendRepository()
    {
        var configPath = WriteConfigFile(SampleLoader.Load("dc.sample.yaml"));
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [FlagKey] = "true",
            [ConfigPathKey] = configPath,
        });

        Assert.IsType<StateBackendHouseholdRepository>(
            provider.GetRequiredService<IHouseholdRepository>());
    }

    // Mock mode serves in-memory data with no state backend at all, so it must win over the
    // configurable-backend flag exactly as it wins over the plugin path today.
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void MockHouseholdData_BeatsTheFlag_HouseholdRepositoryResolvesToMock(string flagValue)
    {
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            ["UseMockHouseholdData"] = "true",
            [FlagKey] = flagValue,
            // A nonexistent path proves mock mode never touches the config-driven stack.
            [ConfigPathKey] = Path.Combine(Path.GetTempPath(), "does-not-exist.yaml"),
        });

        Assert.IsType<MockHouseholdRepository>(
            provider.GetRequiredService<IHouseholdRepository>());
    }

    [Fact]
    public void FlagOn_WithDeclaredHouseholdLookup_PortSharesTheBackend()
    {
        var configPath = WriteConfigFile(SampleLoader.Load("dc.sample.yaml"));
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [FlagKey] = "true",
            [ConfigPathKey] = configPath,
        });

        var ports = provider.GetRequiredService<ConfigurableStateBackendPorts>();

        Assert.IsType<ConfigurableStateBackend>(ports.HouseholdLookup);
        Assert.Same(ports.HouseholdLookup, provider.GetRequiredService<ICardReplacementBackend>());
    }

    [Fact]
    public async Task FlagOn_WithUndeclaredHouseholdLookup_LookupThrowsNotSupported()
    {
        var configPath = WriteConfigFile(CardReplacementOnlyYaml);
        using var provider = BuildProvider(new Dictionary<string, string?>
        {
            [FlagKey] = "true",
            [ConfigPathKey] = configPath,
        });

        var ports = provider.GetRequiredService<ConfigurableStateBackendPorts>();
        Assert.IsType<UnsupportedStateBackendOperation>(ports.HouseholdLookup);

        // The repository still resolves; the undeclared operation fails loud on use.
        var repository = provider.GetRequiredService<IHouseholdRepository>();
        Assert.IsType<StateBackendHouseholdRepository>(repository);

        var ex = await Assert.ThrowsAsync<NotSupportedException>(
            () => repository.GetHouseholdByEmailAsync(
                "u@e.com",
                new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true),
                UserIalLevel.IAL1plus));

        Assert.Contains("Household lookup", ex.Message);
        Assert.Contains("backend configuration", ex.Message);
    }
}
