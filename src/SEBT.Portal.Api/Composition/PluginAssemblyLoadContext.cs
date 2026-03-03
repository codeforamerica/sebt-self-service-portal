using System.Reflection;
using System.Runtime.Loader;

namespace SEBT.Portal.Api.Composition;

/// <summary>
/// Loads plugin assemblies and resolves their dependencies
/// from the plugin directories, so plugin DLLs do not need to be copied to the app base.
/// </summary>
internal sealed class PluginAssemblyLoadContext : AssemblyLoadContext
{
    private readonly string[] _pluginPaths;

    public PluginAssemblyLoadContext(string[] pluginPaths)
        : base(isCollectible: false)
    {
        _pluginPaths = pluginPaths;
        Resolving += OnResolving;
    }

    private Assembly? OnResolving(AssemblyLoadContext context, AssemblyName assemblyName)
    {
        var fileName = assemblyName.Name + ".dll";
        foreach (var dir in _pluginPaths)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                continue;
            var path = Path.Combine(dir, fileName);
            if (File.Exists(path))
                return context.LoadFromAssemblyPath(Path.GetFullPath(path));
        }
        return null;
    }
}
