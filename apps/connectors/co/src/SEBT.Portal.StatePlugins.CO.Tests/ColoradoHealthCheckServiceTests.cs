using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SEBT.Portal.StatePlugins.CO;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoHealthCheckServiceTests
{
    [Fact]
    public async Task AlwaysDegradedHealthCheck_ReturnsDegraded_WithGivenMessage()
    {
        // Arrange
        var message = "CBMS credentials are not configured.";
        var check = new AlwaysDegradedHealthCheck(message);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext());

        // Assert
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(message, result.Description);
    }

    private const string CbmsCheckName = "co-cbms-api-ping";

    [Fact]
    public async Task ConfigureHealthChecks_registers_degraded_check_when_not_configured()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var builder = services.AddHealthChecks();
        // Explicit empty config to override env vars; CbmsOptionsHelper falls back to environment otherwise
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:ClientId"] = "",
                ["Cbms:ClientSecret"] = "",
                ["Cbms:UseMockResponses"] = "false"
            })
            .Build();
        var healthCheckService = new ColoradoHealthCheckService(config);

        healthCheckService.ConfigureHealthChecks(builder);

        var provider = services.BuildServiceProvider();
        var healthCheck = provider.GetRequiredService<HealthCheckService>();
        var result = await healthCheck.CheckHealthAsync();

        Assert.Equal(HealthStatus.Degraded, result.Status);
        var entry = result.Entries[CbmsCheckName];
        Assert.Contains("CBMS credentials are not configured", entry.Description);
    }

    [Fact]
    public async Task ConfigureHealthChecks_registers_CbmsApiHealthCheck_when_UseMockResponses_true()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHybridCache();
        var builder = services.AddHealthChecks();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cbms:UseMockResponses"] = "true"
            })
            .Build();
        var cacheProvider = services.BuildServiceProvider();
        var cache = cacheProvider.GetRequiredService<HybridCache>();
        var healthCheckService = new ColoradoHealthCheckService(config, cache);

        healthCheckService.ConfigureHealthChecks(builder);

        var provider = services.BuildServiceProvider();
        var healthCheck = provider.GetRequiredService<HealthCheckService>();
        var result = await healthCheck.CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }
}
