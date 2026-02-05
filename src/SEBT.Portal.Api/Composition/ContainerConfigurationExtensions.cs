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
            var combinedPath = Path.Combine(AppContext.BaseDirectory, path);

            if (!Directory.Exists(combinedPath))
            {
                continue;
            }

            var assemblies = Directory
                .GetFiles(combinedPath, "*.dll", searchOption)
                .Select(AssemblyLoadContext.Default.LoadFromAssemblyPath);

            containerConfiguration.WithAssemblies(assemblies, conventions);
        }

        return containerConfiguration;
    }
}
