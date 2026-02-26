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
        foreach (var path in paths)
        {
            var combinedPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, path));

            if (!Directory.Exists(combinedPath))
            {
                continue;
            }

            var assemblies = Directory
                .GetFiles(combinedPath, "*.dll", searchOption)
                .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath)
                .ToList();

            containerConfiguration.WithAssemblies(assemblies, conventions);
        }

        return containerConfiguration;
    }
}
