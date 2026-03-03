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

        var defaultAssemblyNames = AssemblyLoadContext.Default.Assemblies
            .Select(a => a.GetName().Name)
            .ToHashSet();

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
