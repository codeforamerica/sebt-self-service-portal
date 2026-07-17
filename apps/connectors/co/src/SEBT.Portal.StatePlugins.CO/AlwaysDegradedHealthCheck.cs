using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SEBT.Portal.StatePlugins.CO;

/// <summary>
/// A health check that always returns <see cref="HealthCheckResult.Degraded(string)"/>.
/// Used to surface misconfiguration (e.g. missing CBMS credentials) in the health endpoint
/// rather than silently omitting the check.
/// </summary>
internal class AlwaysDegradedHealthCheck(string description) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HealthCheckResult.Degraded(description));
    }
}
