namespace SEBT.Portal.Infrastructure.States;

using System.Reflection;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Kernel;
using SEBT.Portal.StateConnector;

/// <summary>
/// Registry implementation for managing state-specific plugins.
/// Discovers and loads plugins from loaded assemblies.
/// </summary>
public class StatePluginRegistry : IStatePluginRegistry
{
    private readonly Dictionary<string, IStatePlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<StatePluginRegistry> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatePluginRegistry"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public StatePluginRegistry(ILogger<StatePluginRegistry> logger)
    {
        _logger = logger;
        DiscoverPlugins();
    }

    /// <inheritdoc />
    public IStatePlugin? GetPlugin(string stateCode)
    {
        _plugins.TryGetValue(stateCode, out var plugin);
        return plugin;
    }

    /// <inheritdoc />
    public IEnumerable<IStatePlugin> GetAllPlugins()
    {
        return _plugins.Values;
    }

    /// <inheritdoc />
    public IStatePlugin? GetActivePlugin()
    {
        var state = Environment.GetEnvironmentVariable("STATE")
            ?? Environment.GetEnvironmentVariable("NEXT_PUBLIC_STATE");

        if (string.IsNullOrEmpty(state))
        {
            return null;
        }

        return GetPlugin(state);
    }

    /// <summary>
    /// Discovers all state plugins from loaded assemblies and plugins directory.
    /// </summary>
    private void DiscoverPlugins()
    {
        try
        {
            var pluginType = typeof(IStatePlugin);

            // First, load plugins from plugins directory (for production deployments)
            LoadPluginsFromDirectory(pluginType);

            // Then, discover plugins from already loaded assemblies (for development with project references)
            DiscoverPluginsFromAssemblies(pluginType);

            _logger.LogInformation("Plugin discovery complete. Found {Count} plugin(s)", _plugins.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during plugin discovery");
        }
    }

    /// <summary>
    /// Loads plugins from a plugins directory.
    /// </summary>
    private void LoadPluginsFromDirectory(Type pluginType)
    {
        var pluginsDirectory = Path.Combine(AppContext.BaseDirectory, "plugins");
        if (!Directory.Exists(pluginsDirectory))
        {
            _logger.LogDebug("Plugins directory not found: {PluginsDirectory}", pluginsDirectory);
            return;
        }

        _logger.LogInformation("Scanning plugins directory: {PluginsDirectory}", pluginsDirectory);

        foreach (var dll in Directory.GetFiles(pluginsDirectory, "SEBT.Portal.States.*.dll", SearchOption.TopDirectoryOnly))
        {
            try
            {
                // Check if file exists and is readable
                if (!File.Exists(dll))
                {
                    _logger.LogWarning("Plugin DLL not found: {Dll}", dll);
                    continue;
                }

                var fileInfo = new FileInfo(dll);
                if (fileInfo.Length == 0)
                {
                    _logger.LogError("Plugin DLL is empty or corrupted: {Dll}", dll);
                    continue;
                }

                // Attempt to load the assembly
                Assembly? assembly = null;
                try
                {
                    assembly = Assembly.LoadFrom(dll);
                }
                catch (BadImageFormatException ex)
                {
                    _logger.LogError(ex, "Plugin DLL has invalid format or is not a .NET assembly: {Dll}", dll);
                    continue;
                }
                catch (FileLoadException ex)
                {
                    _logger.LogError(ex, "Failed to load plugin DLL (possibly missing dependencies): {Dll}. Error: {Message}", dll, ex.Message);
                    continue;
                }
                catch (FileNotFoundException ex)
                {
                    _logger.LogError(ex, "Plugin DLL or its dependencies not found: {Dll}. Missing: {MissingFile}", dll, ex.FileName);
                    continue;
                }

                if (assembly != null)
                {
                    DiscoverPluginsFromAssembly(assembly, pluginType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error loading plugin assembly: {Dll}", dll);
            }
        }
    }

    /// <summary>
    /// Discovers plugins from already loaded assemblies.
    /// </summary>
    private void DiscoverPluginsFromAssemblies(Type pluginType)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in assemblies)
        {
            try
            {
                DiscoverPluginsFromAssembly(assembly, pluginType);
            }
            catch (ReflectionTypeLoadException ex)
            {
                _logger.LogWarning(ex, "Failed to load types from assembly {Assembly}",
                    assembly.GetName().Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing assembly {Assembly}",
                    assembly.GetName().Name);
            }
        }
    }

    /// <summary>
    /// Discovers plugins from a specific assembly.
    /// </summary>
    private void DiscoverPluginsFromAssembly(Assembly assembly, Type pluginType)
    {
        try
        {
            var pluginTypes = assembly.GetTypes()
                .Where(t => pluginType.IsAssignableFrom(t)
                         && !t.IsInterface
                         && !t.IsAbstract);

            foreach (var type in pluginTypes)
            {
                try
                {
                    if (Activator.CreateInstance(type) is IStatePlugin plugin)
                    {
                        var stateCode = plugin.StateCode;

                        // Validate state code is not empty
                        if (string.IsNullOrWhiteSpace(stateCode))
                        {
                            _logger.LogError("Plugin type {Type} from assembly {Assembly} has an empty or null StateCode",
                                type.FullName, assembly.GetName().Name);
                            continue;
                        }

                        if (_plugins.ContainsKey(stateCode))
                        {
                            _logger.LogWarning(
                                "Duplicate plugin found for state {StateCode}. Existing: {ExistingType}, New: {NewType}. Keeping existing plugin.",
                                stateCode, _plugins[stateCode].GetType().Name, type.Name);
                            continue;
                        }

                        // Validate plugin properties
                        if (string.IsNullOrWhiteSpace(plugin.StateName))
                        {
                            _logger.LogError("Plugin for state {StateCode} has an empty or null StateName", stateCode);
                            continue;
                        }

                        if (plugin.Version == null)
                        {
                            _logger.LogError("Plugin for state {StateCode} has a null Version", stateCode);
                            continue;
                        }

                        _plugins[stateCode] = plugin;
                        _logger.LogInformation(
                            "Discovered state plugin: {StateCode} ({StateName}) v{Version} from {Assembly}",
                            plugin.StateCode, plugin.StateName, plugin.Version, assembly.GetName().Name);
                    }
                }
                catch (MissingMethodException ex)
                {
                    _logger.LogError(ex, "Plugin type {Type} from assembly {Assembly} does not have a parameterless constructor",
                        type.FullName, assembly.GetName().Name);
                }
                catch (TargetInvocationException ex)
                {
                    _logger.LogError(ex, "Exception thrown during instantiation of plugin type {Type} from assembly {Assembly}. Inner exception: {InnerException}",
                        type.FullName, assembly.GetName().Name, ex.InnerException?.Message);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to instantiate plugin type {Type} from assembly {Assembly}",
                        type.FullName, assembly.GetName().Name);
                }
            }
        }
        catch (ReflectionTypeLoadException ex)
        {
            _logger.LogWarning(ex, "Failed to load types from assembly {Assembly}",
                assembly.GetName().Name);
        }
    }
}

