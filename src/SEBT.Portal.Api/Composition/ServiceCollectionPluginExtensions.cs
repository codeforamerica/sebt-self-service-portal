// TODO: Remove System.Composition dependency — only referenced for assembly loading reuse
// and because [Export]/[ExportMetadata] attributes remain on plugin classes (inert).
using Serilog;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Api.Composition;

// Plugin Discovery and Registration
//
// Plugins are discovered by scanning assemblies loaded from plugins-{state}/ directories.
// Each plugin must implement exactly one service interface (e.g., ISummerEbtCaseService)
// in addition to IStatePlugin.
//
// Plugins are instantiated by the DI container via ActivatorUtilities — NOT by MEF.
// This means plugin constructors can receive any DI-registered service (IConfiguration,
// ILoggerFactory, HybridCache, etc.) as constructor parameters.
//
// The MEF attributes ([Export], [ExportMetadata], [ImportingConstructor]) on plugin
// classes are currently inert — they are not read or used by this code. They remain
// for now because the plugin assemblies have not been updated to remove them.
//
// Next step: extract assembly loading into a standalone helper and remove the
// System.Composition dependency entirely.
internal static class ServiceCollectionPluginExtensions
{
    public static IServiceCollection AddPlugins(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<IStateAuthenticationService, Defaults.DefaultStateAuthenticationService>();
        services.TryAddSingleton<IStateHealthCheckService, Defaults.DefaultStateHealthCheckService>();
        services.TryAddSingleton<ISummerEbtCaseService, Defaults.DefaultSummerEbtCaseService>();
        services.TryAddSingleton<IEnrollmentCheckService, Defaults.DefaultEnrollmentCheckService>();
        services.TryAddSingleton<IAddressUpdateService, Defaults.DefaultAddressUpdateService>();

        var healthChecksBuilder = services.AddHealthChecks();

        var pluginAssemblyPaths = configuration
                                      .GetSection("PluginAssemblyPaths")
                                      .Get<string[]>()
                                  ?? throw new InvalidOperationException("PluginAssemblyPaths missing from configuration.");
        Log.Information("Loading plugins from: {PluginAssemblyPaths}", pluginAssemblyPaths);

        var loadedAssemblies = PluginAssemblyLoader.LoadAssembliesFromPaths(pluginAssemblyPaths);

        var pluginTypes = loadedAssemblies
            .SelectMany(a =>
            {
                try { return a.GetExportedTypes(); }
                catch (TypeLoadException ex)
                {
                    Log.Warning(ex, "Could not load types from assembly {Assembly}", a.FullName);
                    return [];
                }
            })
            .Where(t => typeof(IStatePlugin).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .ToList();

        foreach (var pluginType in pluginTypes)
        {
            Log.Information("Discovered plugin type: {PluginType}", pluginType.FullName);

            var pluginInterfaces = pluginType.GetInterfaces()
                .Where(i => i != typeof(IStatePlugin))
                .ToList();

            switch (pluginInterfaces.Count)
            {
                case 0:
                    throw new InvalidOperationException(
                        $"Plugin '{pluginType.FullName}' does not implement any interface besides IStatePlugin. " +
                        "Each plugin must implement exactly one service interface in addition to IStatePlugin.");
                case > 1:
                    throw new InvalidOperationException(
                        $"Plugin '{pluginType.FullName}' implements multiple interfaces: " +
                        $"{string.Join(", ", pluginInterfaces.Select(i => i.FullName))}. " +
                        "Each plugin must implement exactly one service interface in addition to IStatePlugin.");
            }

            var pluginInterface = pluginInterfaces[0];

            if (typeof(IStateHealthCheckService).IsAssignableFrom(pluginType))
            {
                // Health check plugins are instantiated eagerly so we can call
                // ConfigureHealthChecks() during service registration (it needs
                // IHealthChecksBuilder, which wraps IServiceCollection).
                //
                // LIMITATION: This builds a temporary IServiceProvider from the current
                // IServiceCollection to resolve constructor dependencies. The temporary
                // provider has its own singleton scope, which works today because health
                // check plugins only depend on IConfiguration and ILoggerFactory — both
                // are already fully constructed at this point and are effectively shared.
                //
                // If a health check plugin ever needs a DI service with shared mutable
                // state (e.g., HybridCache backed by Redis), the temporary provider
                // would create a separate instance, breaking shared-state assumptions.
                // At that point, revisit this approach — e.g., defer health check
                // registration to a post-build step, or use a type-based registration
                // with a lazy resolve adapter.
                using var tempProvider = services.BuildServiceProvider();
                var instance = ActivatorUtilities.CreateInstance(tempProvider, pluginType);
                Log.Information("Constructed health check plugin: {PluginType}", pluginType.FullName);

                ((IStateHealthCheckService)instance).ConfigureHealthChecks(healthChecksBuilder);
                services.AddSingleton(pluginInterface, instance);
            }
            else
            {
                // All other plugins: register as a factory so DI creates them on first
                // resolve using the *real* service provider. This gives plugins access
                // to any DI-registered service via constructor injection.
                var capturedType = pluginType; // avoid closure over loop variable
                services.AddSingleton(pluginInterface, sp =>
                {
                    var logger = sp.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("SEBT.Portal.Api.Composition");
                    logger.LogInformation("Constructing plugin: {PluginType}", capturedType.FullName);
                    return ActivatorUtilities.CreateInstance(sp, capturedType);
                });
            }
        }

        return services;
    }
}
