using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.FeatureManagement;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Api.Composition;

/// <summary>
/// Wraps a connector-registered health check so CBMS/SQL probes do not fail
/// <c>/health</c> while traffic is on <c>ConfigurableStateBackend</c>.
/// </summary>
internal sealed class FeatureGatedPluginHealthCheck(
    IFeatureManager? featureManager,
    Func<IHealthCheck> innerFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (featureManager is not null
            && await featureManager.IsEnabledAsync(FeatureFlags.UseConfigurableStateBackend))
        {
            return HealthCheckResult.Healthy(
                "Skipped while use_configurable_state_backend is on.");
        }

        return await innerFactory().CheckHealthAsync(context, cancellationToken);
    }
}

/// <summary>
/// Intercepts <see cref="IHealthChecksBuilder.Add"/> so every plugin-registered
/// check is wrapped in <see cref="FeatureGatedPluginHealthCheck"/>.
/// </summary>
internal sealed class FeatureGatedPluginHealthChecksBuilder(IHealthChecksBuilder inner) : IHealthChecksBuilder
{
    public IServiceCollection Services => inner.Services;

    public IHealthChecksBuilder Add(HealthCheckRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);

        var wrapped = new HealthCheckRegistration(
            registration.Name,
            sp => new FeatureGatedPluginHealthCheck(
                sp.GetService<IFeatureManager>(),
                () => registration.Factory(sp)),
            registration.FailureStatus,
            registration.Tags,
            registration.Timeout)
        {
            Delay = registration.Delay,
        };

        return inner.Add(wrapped);
    }
}
