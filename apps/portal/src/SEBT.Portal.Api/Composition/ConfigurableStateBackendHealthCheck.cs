using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.FeatureManagement;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.StateBackends;

namespace SEBT.Portal.Api.Composition;

internal sealed class ConfigurableStateBackendHealthCheck(
    IFeatureManager featureManager,
    IStateBackendHealth? backend) : IHealthCheck
{
    public const string CheckName = "configurable-state-backend";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!await featureManager.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend))
        {
            return HealthCheckResult.Healthy("Configurable state backend is not enabled.");
        }

        if (backend is null)
        {
            return HealthCheckResult.Unhealthy(ConfigurableStateBackendGate.MissingConfigMessage);
        }

        StateBackendHealth health = await backend.GetHealthAsync(cancellationToken);
        return health.IsHealthy
            ? HealthCheckResult.Healthy("Configurable state backend responded successfully.")
            : HealthCheckResult.Unhealthy("Configurable state backend probe failed.");
    }
}
