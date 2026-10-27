using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SEBT.Portal.StatePlugins.CO;
using SEBT.Portal.StatePlugins.CO.Cbms;

namespace SEBT.Portal.StatePlugins.CO.Tests;

public class ColoradoHealthCheckServiceTests
{
    private const string CbmsCheckName = "co-cbms-api-ping";

    [Fact]
    public async Task ConfigureHealthChecks_follows_UseMockResponses_on_each_check()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cbms:ClientId"] = "",
            ["Cbms:ClientSecret"] = "",
            [CbmsOptionsHelper.UseMockResponsesConfigurationKey] = "false"
        });
        services.AddSingleton<IConfiguration>(config);
        var builder = services.AddHealthChecks();
        var healthCheckService = new ColoradoHealthCheckService();

        healthCheckService.ConfigureHealthChecks(builder);

        var provider = services.BuildServiceProvider();
        var healthCheck = provider.GetRequiredService<HealthCheckService>();

        var degraded = await healthCheck.CheckHealthAsync();
        Assert.Equal(HealthStatus.Degraded, degraded.Status);
        Assert.Contains("CBMS credentials are not configured", degraded.Entries[CbmsCheckName].Description);

        config[CbmsOptionsHelper.UseMockResponsesConfigurationKey] = "true";
        var healthy = await healthCheck.CheckHealthAsync();
        Assert.Equal(HealthStatus.Healthy, healthy.Status);
    }

    [Fact]
    public async Task ConfigureHealthChecks_mock_ping_does_not_require_hybrid_cache()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationManager();
        config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [CbmsOptionsHelper.UseMockResponsesConfigurationKey] = "true"
        });
        services.AddSingleton<IConfiguration>(config);
        var builder = services.AddHealthChecks();

        new ColoradoHealthCheckService().ConfigureHealthChecks(builder);

        var provider = services.BuildServiceProvider();
        var healthCheck = provider.GetRequiredService<HealthCheckService>();

        var result = await healthCheck.CheckHealthAsync();

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(HealthStatus.Healthy, result.Entries[CbmsCheckName].Status);
    }
}
