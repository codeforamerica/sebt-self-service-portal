using Medallion.Threading;
using StackExchange.Redis;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Infrastructure;
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

        var options = Dependencies.ResolveRedisConfigurationOptions(config, env);

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



    [Fact]
    public void ResolveIHouseholdRepository_WhenUseMockHouseholdDataFalseAndNoPlugin_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            ["UseMockHouseholdData"] = "false"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddPortalInfrastructureRepositories(configuration);
        var provider = services.BuildServiceProvider();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IHouseholdRepository>());
        Assert.Contains("UseMockHouseholdData is false", ex.Message);
        Assert.Contains("no household plugin", ex.Message);
    }

    [Fact]
    public void ResolveIHMACHSHA256Hasher_ResolvesFromAddPortalInfrastructureServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{IdentifierHasherSettings.SectionName}:SecretKey"] = "test-secret-key-that-is-at-least-32-chars!",
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddPortalInfrastructureAppSettings(config);
        services.AddPortalInfrastructureServices(config);

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
                ["Smarty:BaseUrl"] = "https://us-street.api.smartystreets.com",
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddPortalInfrastructureAppSettings(config);
        services.AddPortalInfrastructureServices(config);

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
        Assert.Equal(new Uri("https://us-street.api.smartystreets.com/"), client.BaseAddress);
    }
}
