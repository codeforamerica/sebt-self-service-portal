using System.Reflection;
using System.Runtime.Loader;

namespace SEBT.Portal.Api.Composition;

/// <summary>
/// Loads plugin assemblies and resolves their dependencies from the plugin directories.
/// Shared assemblies (e.g., plugin interfaces) are resolved from the default ALC
/// to preserve type identity between the host and plugins.
/// </summary>
internal sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string[] _pluginPaths;

    public PluginAssemblyLoadContext(string[] pluginPaths)
        : base(isCollectible: false)
    {
        _pluginPaths = pluginPaths;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // If the default ALC already has this assembly, use it to preserve type identity
        // for shared contracts (e.g., ISummerEbtCaseService, IStatePlugin).
        var existing = Default.Assemblies.FirstOrDefault(a => a.GetName().Name == assemblyName.Name);
        if (existing != null)
            return existing;

        // Otherwise resolve from plugin directories (e.g., Microsoft.Kiota.Abstractions).
        var fileName = assemblyName.Name + ".dll";
        foreach (var dir in _pluginPaths)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                continue;
            var path = Path.Combine(dir, fileName);
            if (File.Exists(path))
                return LoadFromAssemblyPath(Path.GetFullPath(path));
        }

        return null;
    }
}
