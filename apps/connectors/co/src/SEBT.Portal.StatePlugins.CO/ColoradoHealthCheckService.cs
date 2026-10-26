using System.Composition;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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
        builder.AddCheck(
            CheckName,
            new ColoradoCbmsHealthCheck(configuration, cache),
            HealthStatus.Unhealthy,
            ["external-api", "co"]);
    }
}
