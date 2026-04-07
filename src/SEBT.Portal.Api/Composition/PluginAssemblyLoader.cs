using System.Reflection;
using System.Runtime.Loader;

namespace SEBT.Portal.Api.Composition;

// Plugin Assembly Loader
//
// Loads plugin assemblies from disk into AssemblyLoadContext.Default.
// This was originally part of the MEF (System.Composition) pipeline but MEF is
// no longer used for composition — plugins are now instantiated by the DI container
// via ActivatorUtilities. The [Export], [ExportMetadata], and [ImportingConstructor]
// attributes on plugin classes are inert but harmless.
//
// TODO: Remove the System.Composition dependency entirely once this approach is stable.
// The assembly loading logic here is standalone and does not depend on MEF.
internal static class PluginAssemblyLoader
{
    public static List<Assembly> LoadAssembliesFromPaths(
        string[] paths,
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var baseDir = AppContext.BaseDirectory;
        var existingPaths = paths
            .Select(p => Path.GetFullPath(Path.Combine(baseDir, p)))
            .Where(Directory.Exists)
            .ToArray();

        if (existingPaths.Length == 0)
            return [];

        // Resolve plugin dependencies (e.g. Kiota) from plugin paths so we can load into Default ALC.
        // Loading into Default ensures plugin types share the same interface types as the host (MEF can discover them).
        Assembly? DefaultResolving(AssemblyLoadContext context, AssemblyName assemblyName)
        {
            var fileName = assemblyName.Name + ".dll";
            foreach (var dir in existingPaths)
            {
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                var path = Path.Combine(dir, fileName);
                if (File.Exists(path))
                    return context.LoadFromAssemblyPath(Path.GetFullPath(path));
            }
            return null;
        }

        // Keep handler registered so plugin types can load dependencies (e.g. Kiota) when first used.
        AssemblyLoadContext.Default.Resolving += DefaultResolving;

        var loadedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Connector post-builds often copy transitive dependencies (contracts, Kiota, etc.) into
        // plugins-* alongside the implementation. Those DLLs are already in the app base or default
        // context — loading them again from the plugin path causes duplicate loads and type/MEF issues.
        var hostAssemblySimpleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assemblyName in AssemblyLoadContext.Default.Assemblies.Select(a => a.GetName().Name))
        {
            if (!string.IsNullOrEmpty(assemblyName))
                hostAssemblySimpleNames.Add(assemblyName);
        }

        foreach (var dllPath in Directory.GetFiles(baseDir, "*.dll"))
        {
            var simpleName = Path.GetFileNameWithoutExtension(dllPath);
            if (!string.IsNullOrEmpty(simpleName))
                hostAssemblySimpleNames.Add(simpleName);
        }

        var allAssemblies = new List<Assembly>();

        foreach (var combinedPath in existingPaths)
        {
            var dllPaths = Directory.GetFiles(combinedPath, "*.dll", searchOption);
            foreach (var dllPath in dllPaths)
            {
                var fullPath = Path.GetFullPath(dllPath);
                var name = Path.GetFileNameWithoutExtension(fullPath);
                if (string.IsNullOrEmpty(name)
                    || hostAssemblySimpleNames.Contains(name)
                    || loadedNames.Contains(name))
                    continue;
                try
                {
                    var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(fullPath);
                    loadedNames.Add(assembly.GetName().Name ?? name);
                    allAssemblies.Add(assembly);
                }
                catch (Exception ex) when (ex is FileLoadException or BadImageFormatException)
                {
                    if (ex.Message.Contains("already loaded", StringComparison.OrdinalIgnoreCase))
                        continue;
                    throw;
                }
            }
        }

        return allAssemblies;
    }
}
