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

        var alc = new PluginAssemblyLoadContext(existingPaths);
        var loadedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var combinedPath in existingPaths)
        {
            var dllPaths = Directory.GetFiles(combinedPath, "*.dll", searchOption);
            var assemblies = new List<Assembly>();

            foreach (var dllPath in dllPaths)
            {
                var fullPath = Path.GetFullPath(dllPath);
                var name = Path.GetFileNameWithoutExtension(fullPath);
                if (loadedNames.Contains(name))
                    continue;
                try
                {
                    var assembly = alc.LoadFromAssemblyPath(fullPath);
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
