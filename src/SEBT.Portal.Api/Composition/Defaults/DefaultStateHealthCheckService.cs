using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Data;

namespace SEBT.Portal.Api.Composition.Defaults;

/// <summary>
/// Default implementation when no state-specific IStateHealthCheckService plugin is loaded.
/// Always reports unhealthy since no backend connectivity can be verified.
/// </summary>
internal class DefaultStateHealthCheckService : IStateHealthCheckService
{
    public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<HealthCheckResult>(
            new HealthCheckResult.Unhealthy("No state health check plugin is configured."));
    }
}
