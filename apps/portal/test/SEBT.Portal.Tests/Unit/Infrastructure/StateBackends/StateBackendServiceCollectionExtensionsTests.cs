using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Infrastructure.StateBackends;
using SEBT.Portal.Infrastructure.StateBackends.Auth;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class StateBackendServiceCollectionExtensionsTests
{
    [Fact]
    public void AddConfigurableStateBackend_WhenConfigPathEmpty_DoesNotRegisterAdapter()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StateBackend:ConfigPath"] = "",
            })
            .Build();
        services.AddSingleton<IConfiguration>(config);

        services.AddConfigurableStateBackend(config);

        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<IHouseholdLookupBackend>());
        Assert.Null(provider.GetService<ConfigurableStateBackend>());
        Assert.NotNull(provider.GetService<IStateBackendSecretResolver>());
    }

    [Fact]
    public void AddConfigurableStateBackend_WhenConfigPathMissing_DoesNotRegisterAdapter()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        services.AddSingleton<IConfiguration>(config);

        services.AddConfigurableStateBackend(config);

        using var provider = services.BuildServiceProvider();
        Assert.Null(provider.GetService<ICardReplacementBackend>());
    }

    [Fact]
    public void AddConfigurableStateBackend_WhenFileMissing_Throws()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["StateBackend:ConfigPath"] = Path.Combine(Path.GetTempPath(), "sebt-missing-backend.yaml"),
            })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(
            () => services.AddConfigurableStateBackend(config));
        Assert.Contains("does not exist", ex.Message);
    }

    [Fact]
    public void AddConfigurableStateBackend_WhenYamlValid_RegistersCorePorts()
    {
        string path = Path.Combine(Path.GetTempPath(), $"sebt-backend-{Guid.NewGuid():N}.yaml");
        File.WriteAllText(path, """
            baseUrl: http://localhost:9
            auth:
              scheme: api_key
              header: X-Api-Key
              keyRef: test-api-key
            operations:
              health:
                method: get
                path: /health
            """);

        try
        {
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["StateBackend:ConfigPath"] = path,
                    ["test-api-key"] = "secret",
                })
                .Build();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton(TimeProvider.System);

            services.AddConfigurableStateBackend(config);

            using var provider = services.BuildServiceProvider();
            var backend = provider.GetRequiredService<ConfigurableStateBackend>();
            Assert.Same(backend, provider.GetRequiredService<IHouseholdLookupBackend>());
            Assert.Same(backend, provider.GetRequiredService<ICardReplacementBackend>());
            Assert.Same(backend, provider.GetRequiredService<IAddressUpdateBackend>());
            Assert.Same(backend, provider.GetRequiredService<IEnrollmentCheckBackend>());
            Assert.Same(backend, provider.GetRequiredService<IStateBackendHealth>());
        }
        finally
        {
            File.Delete(path);
        }
    }
}
