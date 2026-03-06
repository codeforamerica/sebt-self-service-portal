using System.Composition.Convention;
using System.Composition.Hosting;
using SEBT.Portal.StatesPlugins.Interfaces;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SEBT.Portal.Api.Composition;

using Serilog;

internal static class ServiceCollectionPluginExtensions
{
    /// <summary>
    /// Registers MEF plugins from the configured assembly paths. Extends IHostBuilder
    /// (rather than IServiceCollection) so that plugin loading runs during Build(),
    /// after all ConfigureAppConfiguration callbacks have fired. This allows
    /// WebApplicationFactory tests to override plugin paths and connection strings
    /// via ConfigureAppConfiguration instead of process-global environment variables.
    /// </summary>
    public static IHostBuilder AddPlugins(this IHostBuilder hostBuilder)
    {
        hostBuilder.ConfigureServices((context, services) =>
            services.RegisterPlugins(context.Configuration));
        return hostBuilder;
    }

    private static void RegisterPlugins(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<IStateAuthenticationService, Defaults.DefaultIStateAuthenticationService>();
        services.TryAddSingleton<IEnrollmentCheckService, Defaults.DefaultEnrollmentCheckService>();

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

        conventions
            .ForTypesDerivedFrom<IEnrollmentCheckService>()
            .Export<IEnrollmentCheckService>()
            .Shared();

        return new ContainerConfiguration()
            .WithExport(configuration)
            .WithAssembliesInPath(assemblyPaths, conventions);
    }
}
