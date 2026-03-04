using System.Composition.Convention;
using System.Composition.Hosting;
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
            var assemblies = Directory
                .GetFiles(combinedPath, "*.dll", searchOption)
                .Where(p => !defaultAssemblyNames.Contains(Path.GetFileNameWithoutExtension(p)))
                .Select(p => alc.LoadFromAssemblyPath(Path.GetFullPath(p)))
                .ToList();

            containerConfiguration.WithAssemblies(assemblies, conventions);
        }

        return containerConfiguration;
    }
}
