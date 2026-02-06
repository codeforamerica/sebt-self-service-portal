using System.Composition.Convention;
using System.Composition.Hosting;
using SEBT.Portal.StatesPlugins.Interfaces;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SEBT.Portal.Api.Composition;

using Serilog;

internal static class ServiceCollectionPluginExtensions
{
    public static IServiceCollection AddPlugins(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<IStateAuthenticationService, Defaults.DefaultIStateAuthenticationService>();

        var pluginAssemblyPaths = configuration
                                      .GetSection("PluginAssemblyPaths")
                                      .Get<string[]>()
                                  ?? throw new InvalidOperationException("PluginAssemblyPaths missing from configuration.");
        Log.Information("Loading plugins from: {PluginAssemblyPaths}", pluginAssemblyPaths);
        var containerConfiguration = CreateContainerConfiguration(pluginAssemblyPaths);
        using var container = containerConfiguration.CreateContainer();

        var plugins = container.GetExports<IStatePlugin>();

        foreach (var plugin in plugins)
        {
            Log.Information("Configuring services for plugin: {PluginType}", plugin.GetType().FullName);
            var pluginInterfaces = plugin.GetType().GetInterfaces()
                .Where(i => i != typeof(IStatePlugin))
                .ToList();

            switch (pluginInterfaces.Count)
            {
                case 0:
                    throw new InvalidOperationException($"Plugin '{plugin.GetType().FullName}' does not implement any interface besides IStatePlugin. " +
                                                        "Each plugin must implement exactly one service interface in addition to IStatePlugin.");
                case > 1:
                    throw new InvalidOperationException($"Plugin '{plugin.GetType().FullName}' implements multiple interfaces: " +
                                                        $"{string.Join(", ", pluginInterfaces.Select(i => i.FullName))}. " +
                                                        "Each plugin must implement exactly one service interface in addition to IStatePlugin.");
                default:
                    services.AddSingleton(pluginInterfaces[0], plugin);
                    break;
            }
        }

        return services;
    }

    private static ContainerConfiguration CreateContainerConfiguration(string[] assemblyPaths)
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

        return new ContainerConfiguration().WithAssembliesInPath(assemblyPaths, conventions);
    }
}
