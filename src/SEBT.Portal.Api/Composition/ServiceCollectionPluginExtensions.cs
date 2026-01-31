using System.Composition.Convention;
using System.Composition.Hosting;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Api.Composition;

using Serilog;

internal static class ServiceCollectionPluginExtensions
{
    public static IServiceCollection AddPlugins(this IServiceCollection services, IConfiguration configuration)
    {
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
            var @interface = plugin.GetType().GetInterfaces().Single(i => i != typeof(IStatePlugin));
            services.AddSingleton(@interface, plugin);
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
