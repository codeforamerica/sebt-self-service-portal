using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.FeatureManagement;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Api.Composition;

internal static class FeatureGatedStateBackendExtensions
{
    /// <summary>
    /// Replaces the plugin-interface registrations with request-time gates so AppConfig can
    /// flip household lookup, writes, enrollment, and health onto <c>ConfigurableStateBackend</c>
    /// without a restart. The MEF connector stays reachable via keyed DI
    /// (<see cref="StatePluginKeys.Connector"/>).
    /// </summary>
    public static IServiceCollection AddFeatureGatedStateBackendRouting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<ISummerEbtCaseService>(sp =>
            new FeatureGatedSummerEbtCaseService(
                sp.GetRequiredService<IFeatureManager>(),
                sp.GetRequiredKeyedService<ISummerEbtCaseService>(StatePluginKeys.Connector),
                sp.GetService<IHouseholdLookupBackend>()));

        services.AddSingleton<ICardReplacementService>(sp =>
            new FeatureGatedCardReplacementService(
                sp.GetRequiredService<IFeatureManager>(),
                sp.GetRequiredKeyedService<ICardReplacementService>(StatePluginKeys.Connector),
                sp.GetService<ICardReplacementBackend>()));

        services.AddSingleton<IEnrollmentCheckService>(sp =>
            new FeatureGatedEnrollmentCheckService(
                sp.GetRequiredService<IFeatureManager>(),
                sp.GetRequiredKeyedService<IEnrollmentCheckService>(StatePluginKeys.Connector),
                sp.GetService<IEnrollmentCheckBackend>()));

        bool useMockHouseholdData = configuration.GetValue("UseMockHouseholdData", false);
        if (!useMockHouseholdData)
        {
            services.AddSingleton<IAddressUpdateService>(sp =>
                new FeatureGatedAddressUpdateService(
                    sp.GetRequiredService<IFeatureManager>(),
                    sp.GetRequiredKeyedService<IAddressUpdateService>(StatePluginKeys.Connector),
                    sp.GetService<IAddressUpdateBackend>()));
        }

        services.AddHealthChecks().Add(new HealthCheckRegistration(
            ConfigurableStateBackendHealthCheck.CheckName,
            sp => new ConfigurableStateBackendHealthCheck(
                sp.GetRequiredService<IFeatureManager>(),
                sp.GetService<IStateBackendHealth>()),
            failureStatus: HealthStatus.Unhealthy,
            tags: ["external-api", "state-backend"]));

        return services;
    }
}
