using System.Composition.Convention;
using System.Composition.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Api.Composition;

using Serilog;

internal static class ServiceCollectionPluginExtensions
{
    public static IServiceCollection AddPlugins(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<IStateAuthenticationService, Defaults.DefaultIStateAuthenticationService>();
        services.TryAddSingleton<ISummerEbtCaseService, Defaults.DefaultSummerEbtCaseService>();

        var pluginAssemblyPaths = configuration
                                      .GetSection("PluginAssemblyPaths")
                                      .Get<string[]>()
                                  ?? throw new InvalidOperationException("PluginAssemblyPaths missing from configuration.");
        Log.Information("Loading plugins from: {PluginAssemblyPaths}", pluginAssemblyPaths);

        // Resolve store and accessor so the CO plugin can satisfy its [Import] for IStateAuthStore (and accessor if needed).
        IStateAuthStore store;
        IStateAuthSessionAccessor accessor;
        using (var tempProvider = services.BuildServiceProvider())
        {
            store = tempProvider.GetRequiredService<IStateAuthStore>();
            accessor = tempProvider.GetRequiredService<IStateAuthSessionAccessor>();
        }
        var containerConfiguration = CreateContainerConfiguration(pluginAssemblyPaths, configuration, store, accessor);
        using var container = containerConfiguration.CreateContainer();

        var plugins = container.GetExports<IStatePlugin>();
        var oidcExports = container.GetExports<IStateOidcLoginService>().ToList();
        Log.Information("Found {Count} OIDC login plugin(s)", oidcExports.Count);

        foreach (var oidcService in oidcExports)
        {
            var stateCode = oidcService.StateCode;
            if (string.IsNullOrEmpty(stateCode))
            {
                Log.Warning("OIDC login plugin {Type} has empty StateCode; skipping", oidcService.GetType().FullName);
                continue;
            }
            var key = stateCode.ToLowerInvariant();
            services.AddKeyedSingleton<IStateOidcLoginService>(key, oidcService);
            Log.Information("Registered OIDC login for state: {StateCode}", stateCode);
        }

        foreach (var plugin in plugins)
        {
            var pluginType = plugin.GetType();
            if (plugin is IStateOidcLoginService)
                continue;

            Log.Information("Configuring services for plugin: {PluginType}", pluginType.FullName);
            var pluginInterfaces = pluginType.GetInterfaces()
                .Where(i => i != typeof(IStatePlugin))
                .ToList();

            switch (pluginInterfaces.Count)
            {
                case 0:
                    throw new InvalidOperationException($"Plugin '{pluginType.FullName}' does not implement any interface besides IStatePlugin. " +
                                                        "Each plugin must implement exactly one service interface in addition to IStatePlugin.");
                case > 1:
                    throw new InvalidOperationException($"Plugin '{pluginType.FullName}' implements multiple interfaces: " +
                                                        $"{string.Join(", ", pluginInterfaces.Select(i => i.FullName))}. " +
                                                        "Each plugin must implement exactly one service interface in addition to IStatePlugin.");
                default:
                    services.AddSingleton(pluginInterfaces[0], plugin);
                    break;
            }
        }

        // Ensure the app uses the same store/accessor instances the plugins received (single shared store).
        services.AddSingleton(store);
        services.AddSingleton(accessor);

        return services;
    }

    private static ContainerConfiguration CreateContainerConfiguration(
        string[] assemblyPaths,
        IConfiguration configuration,
        IStateAuthStore store,
        IStateAuthSessionAccessor accessor)
    {
        var conventions = new ConventionBuilder();

        conventions
            .ForTypesDerivedFrom<IStateMetadataService>()
            .Export<IStateMetadataService>()
            .Shared();

        conventions
            .ForTypesDerivedFrom<IStateAuthenticationService>()
            .Export<IStateAuthenticationService>()
            .Shared();

        conventions
            .ForTypesDerivedFrom<ISummerEbtCaseService>()
            .Export<ISummerEbtCaseService>()
            .Shared();

        conventions
            .ForTypesDerivedFrom<IStateOidcLoginService>()
            .Export<IStateOidcLoginService>()
            .Shared();

        conventions
            .ForTypesDerivedFrom<IStateAuthService>()
            .Export<IStateAuthService>()
            .Shared();

        return new ContainerConfiguration()
            .WithExport(configuration)
            .WithExport(store)
            .WithExport(accessor)
            .WithAssembliesInPath(assemblyPaths, conventions);
    }
}
