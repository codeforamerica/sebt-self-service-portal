using System.Composition.Convention;
using System.Composition.Hosting;
using System.Reflection;
using System.Runtime.Loader;

namespace SEBT.Portal.Api.Composition;

internal static class ContainerConfigurationExtensions
{
    public static ContainerConfiguration WithAssembliesInPath(
        this ContainerConfiguration containerConfiguration,
        string[] paths,
        AttributedModelProvider conventions,
        SearchOption searchOption = SearchOption.TopDirectoryOnly)
    {
        var baseDir = AppContext.BaseDirectory;
        var existingPaths = paths
            .Select(p => Path.GetFullPath(Path.Combine(baseDir, p)))
            .Where(Directory.Exists)
            .ToArray();

        if (existingPaths.Length == 0)
            return containerConfiguration;

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

        foreach (var combinedPath in existingPaths)
        {
            var dllPaths = Directory.GetFiles(combinedPath, "*.dll", searchOption);
            var assemblies = new List<Assembly>();
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
                    assemblies.Add(assembly);
                }
                catch (Exception ex) when (ex is FileLoadException or BadImageFormatException)
                {
                    if (ex.Message.Contains("already loaded", StringComparison.OrdinalIgnoreCase))
                        continue;
                    throw;
                }
            }
            if (assemblies.Count > 0)
                containerConfiguration.WithAssemblies(assemblies, conventions);
        }

        return containerConfiguration;
    }
}
