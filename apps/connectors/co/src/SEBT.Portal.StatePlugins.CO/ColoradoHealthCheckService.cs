using System.Composition;
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
public class ColoradoHealthCheckService : IStateHealthCheckService
{
    private const string CheckName = "co-cbms-api-ping";

    public void ConfigureHealthChecks(IHealthChecksBuilder builder)
    {
        // Resolve when the check runs. Startup calls this from a provider that
        // is disposed as soon as registration returns.
        builder.Add(new HealthCheckRegistration(
            CheckName,
            sp => new ColoradoCbmsHealthCheck(sp.GetService<IConfiguration>()),
            failureStatus: HealthStatus.Unhealthy,
            tags: ["external-api", "co"]));
    }
}
