using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure;
using SEBT.Portal.Infrastructure.Configuration;
using SEBT.Portal.Infrastructure.Extensions;
using SEBT.Portal.Infrastructure.Repositories;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.Tests.Unit.Infrastructure;

/// <summary>
/// Unit tests for <see cref="Dependencies"/> (service registration).
/// </summary>
public class DependenciesTests
{
    // ---------------------------------------------------------------------------
    // AddCaching — Redis resolution priority
    // ---------------------------------------------------------------------------

    [Fact]
    public void AddCaching_WithRedisHostSettings_RegistersRedisDistributedCache()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Host"] = "localhost"
            })
            .Build();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");

        services.AddCaching(config, env);

        var cacheDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDistributedCache));
        Assert.NotNull(cacheDescriptor);
        Assert.NotEqual("MemoryDistributedCache", cacheDescriptor.ImplementationType?.Name);
    }

    [Fact]
    public void AddCaching_WithLegacyConnectionString_RegistersRedisDistributedCache()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Redis"] = "localhost:6379"
            })
            .Build();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");

        services.AddCaching(config, env);

        var cacheDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDistributedCache));
        Assert.NotNull(cacheDescriptor);
        Assert.NotEqual("MemoryDistributedCache", cacheDescriptor.ImplementationType?.Name);
    }

    [Fact]
    public void ResolveRedisConfigurationOptions_WithBothStructuredAndLegacy_PrefersStructuredHost()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Host"] = "structured-host",
                ["ConnectionStrings:Redis"] = "legacy-host:6379"
            })
            .Build();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        var options = config.ResolveRedisConfigurationOptions(env);

        Assert.NotNull(options);
        Assert.Contains(options.EndPoints, ep => ep.ToString()!.Contains("structured-host"));
        Assert.DoesNotContain(options.EndPoints, ep => ep.ToString()!.Contains("legacy-host"));
    }

    [Fact]
    public void AddCaching_WithoutAnyRedisConfig_InDevelopment_RegistersMemoryDistributedCache()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        services.AddCaching(config, env);

        var cacheDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDistributedCache));
        Assert.NotNull(cacheDescriptor);
        Assert.Equal("MemoryDistributedCache", cacheDescriptor.ImplementationType?.Name);
    }

    // The CO integration e2e stack loads appsettings.co.example.json, which configures TLS Redis, and
    // sets Redis__Host to blank to run on in-memory stores instead.
    [Fact]
    public void AddCaching_WithBlankHostOverConfiguredRedisSection_RegistersMemoryDistributedCache()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Host"] = "",
                ["Redis:Port"] = "6380",
                ["Redis:Ssl"] = "true",
                ["Redis:SslHost"] = "redis",
                ["Redis:AcceptSelfSignedCertificates"] = "true"
            })
            .Build();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        services.AddCaching(config, env);

        var cacheDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDistributedCache));
        Assert.NotNull(cacheDescriptor);
        Assert.Equal("MemoryDistributedCache", cacheDescriptor.ImplementationType?.Name);
    }

    [Fact]
    public void AddCaching_WithoutAnyRedisConfig_NonDevelopmentWithOidc_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Oidc:DiscoveryEndpoint"] = "https://auth.example.com/.well-known/openid-configuration"
            })
            .Build();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Production");

        Assert.Throws<InvalidOperationException>(() => services.AddCaching(config, env));
    }

    [Theory]
    [InlineData(true, "Development", true)]
    [InlineData(true, "Test", false)]
    [InlineData(true, "Production", false)]
    [InlineData(false, "Development", true)]
    [InlineData(false, "Test", true)]
    [InlineData(false, "Production", true)]
    public void AddCaching_AcceptSelfSignedCertificates_ValidatesOnlyDevelopment(
        bool acceptSelfSignedCertificates,
        string environmentName,
        bool shouldSucceed)
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Redis:Host"] = "localhost",
                ["Redis:Port"] = "6380",
                ["Redis:Ssl"] = "true",
                ["Redis:SslHost"] = "redis",
                ["Redis:AcceptSelfSignedCertificates"] = acceptSelfSignedCertificates.ToString()
            })
            .Build();
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(environmentName);

        if (!shouldSucceed)
        {
            Assert.Throws<OptionsValidationException>(() => services.AddCaching(config, env));
        }
        else
        {
            services.AddCaching(config, env);

            var cacheDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IDistributedCache));
            Assert.NotNull(cacheDescriptor);
            Assert.Equal("RedisCacheImpl", cacheDescriptor.ImplementationType?.Name);
        }
    }


    [Fact]
    public void ResolveIHouseholdRepository_WhenUseMockHouseholdDataFalseAndNoPlugin_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            [$"FeatureManagement:{FeatureFlags.UseMockHouseholdData}"] = "false"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(HostEnvironment(Environments.Development));
        services.AddRepositories();
        var provider = services.BuildServiceProvider();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IHouseholdRepository>());
        Assert.Contains("use_mock_household_data", ex.Message);
        Assert.Contains("no household plugin", ex.Message);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void ResolveIHouseholdRepository_WhenFlagOnOutsideProduction_UsesMock(string environmentName)
    {
        var repository = ResolveHouseholdRepository(environmentName, mockFlag: true);

        Assert.IsType<MockHouseholdRepository>(repository);
    }

    [Fact]
    public void ResolveIHouseholdRepository_WhenFlagOn_LogsThatRequestIsServedFromMock()
    {
        var logs = new List<(LogLevel Level, string Message)>();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(new HouseholdMockLogCollector(logs)));
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"FeatureManagement:{FeatureFlags.UseMockHouseholdData}"] = "true"
            })
            .Build());
        services.AddSingleton(HostEnvironment(Environments.Development));
        services.AddSingleton(Substitute.For<ISummerEbtCaseService>());
        services.AddRepositories();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IHouseholdRepository>();

        Assert.Contains(
            logs,
            entry => entry.Level == LogLevel.Warning
                && entry.Message.Contains("served from mock data", StringComparison.Ordinal)
                && entry.Message.Contains(FeatureFlags.UseMockHouseholdData, StringComparison.Ordinal));
    }

    [Fact]
    public void ResolveIHouseholdRepository_WhenProductionAndFlagOn_UsesLive()
    {
        var repository = ResolveHouseholdRepository(Environments.Production, mockFlag: true);

        Assert.IsType<HouseholdRepository>(repository);
    }

    [Fact]
    public void ResolveHouseholdRepository_WhenMockFlagChanges_FollowsFlagValue()
    {
        var configuration = new ConfigurationManager();
        configuration[$"FeatureManagement:{FeatureFlags.UseMockHouseholdData}"] = "true";

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton(HostEnvironment(Environments.Development));
        services.AddSingleton(Substitute.For<ISummerEbtCaseService>());
        services.AddRepositories();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<MockHouseholdRepository>(provider.GetRequiredService<IHouseholdRepository>());

        configuration[$"FeatureManagement:{FeatureFlags.UseMockHouseholdData}"] = "false";

        Assert.IsType<HouseholdRepository>(provider.GetRequiredService<IHouseholdRepository>());
    }

    [Fact]
    public void HouseholdMockGate_WhenProductionAndFlagOn_LogsWarningAndReturnsFalse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"FeatureManagement:{FeatureFlags.UseMockHouseholdData}"] = "true"
            })
            .Build();
        var logger = Substitute.For<ILogger>();

        var enabled = HouseholdMockGate.IsEnabled(
            configuration,
            HostEnvironment(Environments.Production),
            logger);

        Assert.False(enabled);
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains(FeatureFlags.UseMockHouseholdData)),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    private static IHouseholdRepository ResolveHouseholdRepository(string environmentName, bool mockFlag)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"FeatureManagement:{FeatureFlags.UseMockHouseholdData}"] = mockFlag ? "true" : "false"
            })
            .Build());
        services.AddSingleton(HostEnvironment(environmentName));
        services.AddSingleton(Substitute.For<ISummerEbtCaseService>());
        services.AddRepositories();

        return services.BuildServiceProvider().GetRequiredService<IHouseholdRepository>();
    }

    private static IHostEnvironment HostEnvironment(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        return environment;
    }

    [Fact]
    public void ResolveIHMACHSHA256Hasher_ResolvesFromAddServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{IdentifierHasherSettings.SectionName}:SecretKey"] = "test-secret-key-that-is-at-least-32-chars!",
            })
            .Build();

        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns("Development");

        services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IHostEnvironment>(env);
        services.AddLogging();
        services.AddAppSettings(config);
        services.AddServices(config);

        var provider = services.BuildServiceProvider();

        // Act
        var hasher = provider.GetRequiredService<IHMACSHA256Hasher>();

        // Assert
        Assert.NotNull(hasher);
    }

    [Fact]
    public void CreateSmartyHttpClient_CanBeCreatedFromScope_WhenSmartyEnabled()
    {
        // Arrange — build a real DI container with Smarty enabled and scope
        // validation on, mimicking how ASP.NET Core validates service lifetimes.
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smarty:Enabled"] = "true",
                ["Smarty:AuthId"] = "test-id",
                ["Smarty:AuthToken"] = "test-token",
                ["Smarty:BaseUrl"] = "https://us-street.api.smarty.com",
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddAppSettings(config);
        services.AddServices(config);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        // Act — creating the named HttpClient triggers the configuration delegate
        // which must resolve options from the root provider.
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("Smarty");

        // Assert
        Assert.NotNull(client);
        Assert.Equal(new Uri("https://us-street.api.smarty.com/"), client.BaseAddress);
    }

    // ---------------------------------------------------------------------------
    // IBlockedAddressDataSource — selected by AddressValidationData:BlockedAddressFile
    // ---------------------------------------------------------------------------

    [Fact]
    public void ResolveBlockedAddressDataSource_WhenBlockedAddressFileConfigured_LoadsEmbeddedCsv()
    {
        var provider = BuildProviderWithBlockedAddressFile("co-undeliverable-addresses.csv");

        var source = provider.GetRequiredService<IBlockedAddressDataSource>();

        Assert.IsType<CsvBlockedAddressDataSource>(source);
        Assert.NotEmpty(source.GetEntries());
    }

    [Fact]
    public void ResolveBlockedAddressDataSource_WhenBlockedAddressFileNotConfigured_UsesEmptySource()
    {
        var provider = BuildProviderWithBlockedAddressFile(null);

        var source = provider.GetRequiredService<IBlockedAddressDataSource>();

        Assert.IsType<EmptyBlockedAddressDataSource>(source);
    }

    private static ServiceProvider BuildProviderWithBlockedAddressFile(string? blockedAddressFile)
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{AddressValidationDataSettings.SectionName}:BlockedAddressFile"] = blockedAddressFile,
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddAppSettings(config);
        services.AddServices(config);

        return services.BuildServiceProvider();
    }

    private sealed class HouseholdMockLogCollector(List<(LogLevel Level, string Message)> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new Collector(entries);

        public void Dispose()
        {
        }

        private sealed class Collector(List<(LogLevel Level, string Message)> entries) : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) where TState : notnull => new Scope();

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                entries.Add((logLevel, formatter(state, exception)));
            }

            private sealed class Scope : IDisposable
            {
                public void Dispose()
                {
                }
            }
        }
    }
}
