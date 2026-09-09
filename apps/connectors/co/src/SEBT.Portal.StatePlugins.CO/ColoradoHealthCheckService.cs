using System.Composition;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Mocks;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// Colorado health check: registers a check that verifies connectivity
/// to the CBMS SEBT API via its ping endpoint.
/// </summary>
[Export(typeof(IStatePlugin))]
[ExportMetadata("StateCode", "CO")]
[method: ImportingConstructor]
public class ColoradoHealthCheckService(
    [Import(AllowDefault = true)] IConfiguration? configuration = null,
    [Import(AllowDefault = true)] HybridCache? cache = null)
    : IStateHealthCheckService
{
    private const string CheckName = "co-cbms-api-ping";

    public void ConfigureHealthChecks(IHealthChecksBuilder builder)
    {
        var options = CbmsOptionsHelper.GetCbmsOptions(configuration);

        if (!options.IsConfigured)
        {
            builder.AddCheck(
                CheckName,
                new AlwaysDegradedHealthCheck(
                    "CBMS credentials are not configured. " +
                    "Set Cbms:ClientId and Cbms:ClientSecret in appsettings or Cbms__ClientId and Cbms__ClientSecret environment variables. " +
                    "Or set Cbms:UseMockResponses=true for local development with mock responses."),
                HealthStatus.Degraded,
                ["external-api", "co"]);
            return;
        }

        HttpMessageHandler? handler = null;
        var effectiveClientId = options.UseMockResponses ? "mock-client-id" : options.ClientId;
        var effectiveClientSecret = options.UseMockResponses ? "mock-client-secret" : options.ClientSecret;

        if (options.UseMockResponses)
        {
            if (cache == null)
            {
                throw new InvalidOperationException(
                    "HybridCache must be registered in DI when Cbms:UseMockResponses is enabled. " +
                    "Ensure services.AddHybridCache() is called during service registration.");
            }

            var dataStore = new MockCbmsDataStore(cache);
            handler = new MockCbmsHttpHandler(dataStore);
        }

        builder.AddCheck(
            CheckName,
            new CbmsApiHealthCheck(effectiveClientId, effectiveClientSecret, options.ApiBaseUrl, options.TokenEndpointUrl, handler),
            HealthStatus.Unhealthy,
            ["external-api", "co"]);
    }
}
