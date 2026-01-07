using SEBT.Portal.StateConnector;

namespace SEBT.Portal.Kernel;

/// <summary>
/// Registry for managing state-specific plugins.
/// This interface is in Kernel to allow business logic layers (UseCases) to depend on it
/// without depending on Infrastructure, following Clean Architecture principles.
/// </summary>
public interface IStatePluginRegistry
{
    /// <summary>
    /// Gets the plugin for the specified state code, if available.
    /// </summary>
    /// <param name="stateCode">The two-letter state code (e.g., "DC", "CO").</param>
    /// <returns>The state plugin, or null if not found.</returns>
    IStatePlugin? GetPlugin(string stateCode);

    /// <summary>
    /// Gets all registered plugins.
    /// </summary>
    /// <returns>A collection of all registered state plugins.</returns>
    IEnumerable<IStatePlugin> GetAllPlugins();

    /// <summary>
    /// Gets the currently active plugin based on the STATE environment variable.
    /// </summary>
    /// <returns>The active state plugin, or null if not found.</returns>
    IStatePlugin? GetActivePlugin();
}

