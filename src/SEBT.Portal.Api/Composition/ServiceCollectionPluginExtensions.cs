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

        var containerConfiguration = CreateContainerConfiguration(pluginAssemblyPaths, configuration);
        using var container = containerConfiguration.CreateContainer();

        var plugins = container.GetExports<IStatePlugin>();

        foreach (var plugin in plugins)
        {
            var pluginType = plugin.GetType();
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

        return services;
    }

    private static ContainerConfiguration CreateContainerConfiguration(string[] assemblyPaths, IConfiguration configuration)
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

        return new ContainerConfiguration()
            .WithExport(configuration)
            .WithAssembliesInPath(assemblyPaths, conventions);
    }
}
