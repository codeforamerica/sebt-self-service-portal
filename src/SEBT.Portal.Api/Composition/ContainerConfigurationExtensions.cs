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

        // Host owned assemblies should not be loaded by the plugin context,
        // so we exclude them in the filter below
        var defaultAssemblyNames = AssemblyLoadContext.Default.Assemblies
            .Select(a => a.GetName().Name)
            .ToHashSet();

        // Catch for lazily loaded assemblies that are not yet loaded into the default context
        // but are present in the base directory
        foreach (var dll in Directory.GetFiles(baseDir, "*.dll"))
        {
            defaultAssemblyNames.Add(Path.GetFileNameWithoutExtension(dll));
        }

        foreach (var combinedPath in existingPaths)
        {
            var dllPaths = Directory.GetFiles(combinedPath, "*.dll", searchOption);
            var assemblies = new List<Assembly>();
            foreach (var dllPath in dllPaths)
            {
                var fullPath = Path.GetFullPath(dllPath);
                var name = Path.GetFileNameWithoutExtension(fullPath);
                if (defaultAssemblyNames.Contains(name) || loadedNames.Contains(name))
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
