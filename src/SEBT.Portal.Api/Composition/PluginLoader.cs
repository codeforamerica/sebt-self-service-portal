using System.Composition.Convention;
using System.Composition.Hosting;
using SEBT.Portal.StatesPlugins.Interfaces;
using Serilog;

namespace SEBT.Portal.Api.Composition;

/// <summary>
/// Lazily loads MEF plugin assemblies and discovers exports on first access.
/// Takes IConfiguration via constructor injection from DI — at resolution time,
/// this includes all registered config sources (including WAF's
/// ConfigureAppConfiguration overrides in tests).
/// </summary>
internal sealed class PluginLoader
{
    private readonly Lazy<IReadOnlyDictionary<Type, object>> _exports;

    public PluginLoader(IConfiguration configuration)
    {
        _exports = new Lazy<IReadOnlyDictionary<Type, object>>(
            () => LoadExports(configuration));
    }

    /// <summary>
    /// Returns the plugin export for the given interface type, or null if no
    /// plugin provides it.
    /// </summary>
    public T? GetExport<T>() where T : class
    {
        _exports.Value.TryGetValue(typeof(T), out var export);
        return export as T;
    }

    private static IReadOnlyDictionary<Type, object> LoadExports(
        IConfiguration configuration)
    {
        var pluginAssemblyPaths = configuration
                                      .GetSection("PluginAssemblyPaths")
                                      .Get<string[]>()
                                  ?? throw new InvalidOperationException(
                                      "PluginAssemblyPaths missing from configuration.");

        Log.Information("Loading plugins from: {PluginAssemblyPaths}", pluginAssemblyPaths);

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

        using var container = new ContainerConfiguration()
            .WithExport(configuration)
            .WithAssembliesInPath(pluginAssemblyPaths, conventions)
            .CreateContainer();

        var plugins = container.GetExports<IStatePlugin>();
        var exports = new Dictionary<Type, object>();

        foreach (var plugin in plugins)
        {
            Log.Information("Loaded plugin: {PluginType}", plugin.GetType().FullName);

            var pluginInterfaces = plugin.GetType().GetInterfaces()
                .Where(i => i != typeof(IStatePlugin))
                .ToList();

            switch (pluginInterfaces.Count)
            {
                case 0:
                    throw new InvalidOperationException(
                        $"Plugin '{plugin.GetType().FullName}' does not implement any interface besides IStatePlugin. " +
                        "Each plugin must implement exactly one service interface in addition to IStatePlugin.");
                case > 1:
                    throw new InvalidOperationException(
                        $"Plugin '{plugin.GetType().FullName}' implements multiple interfaces: " +
                        $"{string.Join(", ", pluginInterfaces.Select(i => i.FullName))}. " +
                        "Each plugin must implement exactly one service interface in addition to IStatePlugin.");
                default:
                    exports[pluginInterfaces[0]] = plugin;
                    break;
            }
        }

        return exports;
    }
}
