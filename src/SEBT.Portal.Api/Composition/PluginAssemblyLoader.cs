using System.Reflection;
using System.Runtime.Loader;
using Serilog;

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
    /// <param name="paths">Entries from configuration (relative to the API output or content root, or absolute).</param>
    /// <param name="searchOption">Whether to scan subdirectories when counting and loading DLLs.</param>
    /// <param name="contentRootPath">Host content root (project directory during <c>dotnet run</c> / watch). Used when plugin DLLs exist under <c>plugins-*</c> there but were not copied into <c>AppContext.BaseDirectory</c>.</param>
    public static List<Assembly> LoadAssembliesFromPaths(
        string[] paths,
        SearchOption searchOption = SearchOption.TopDirectoryOnly,
        string? contentRootPath = null)
    {
        var baseDir = AppContext.BaseDirectory;
        var existingPaths = ResolvePluginDirectoriesWithDlls(paths, baseDir, contentRootPath, searchOption);

        if (paths.Length > 0 && existingPaths.Length == 0)
        {
            Log.Warning(
                "No plugin directories containing DLLs were found. PluginAssemblyPaths: {ConfiguredPaths}. " +
                "BaseDirectory: {BaseDirectory}. ContentRoot: {ContentRoot}. " +
                "For local dev, build the state connector (e.g. pnpm api:build-co) so DLLs exist under src/SEBT.Portal.Api/plugins-* or under the API output folder.",
                paths,
                baseDir,
                contentRootPath ?? "(not provided)");
        }

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

        // Skip only assemblies already loaded in the default context. Do NOT treat every *.dll file
        // under BaseDirectory as loaded: connector outputs are copied next to the host EXE via
        // CopyToOutputDirectory, so SEBT.Portal.StatePlugins.*.dll may exist on disk without being
        // loaded — those must still load from the plugins-* folder or plugin types never register.
        // (Loading the same assembly twice from different paths still trips the catch below.)
        var hostAssemblySimpleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var assemblyName in AssemblyLoadContext.Default.Assemblies.Select(a => a.GetName().Name))
        {
            if (!string.IsNullOrEmpty(assemblyName))
                hostAssemblySimpleNames.Add(assemblyName);
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

    /// <summary>
    /// Picks one directory per configured path: first tries the app output (<paramref name="baseDir"/>),
    /// then host content root, skipping folders that do not exist or contain no DLLs.
    /// Absolute <paramref name="paths"/> entries are used as-is.
    /// </summary>
    private static string[] ResolvePluginDirectoriesWithDlls(
        string[] paths,
        string baseDir,
        string? contentRootPath,
        SearchOption searchOption)
    {
        var chosen = new List<string>();

        foreach (var raw in paths)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var trimmed = raw.Trim();
            var candidates = new List<string>();
            if (Path.IsPathRooted(trimmed))
            {
                candidates.Add(Path.GetFullPath(trimmed));
            }
            else
            {
                candidates.Add(Path.GetFullPath(Path.Combine(baseDir, trimmed)));
                if (!string.IsNullOrEmpty(contentRootPath))
                    candidates.Add(Path.GetFullPath(Path.Combine(contentRootPath, trimmed)));
            }

            string? existing = null;
            foreach (var dir in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(dir))
                    continue;
                try
                {
                    if (Directory.GetFiles(dir, "*.dll", searchOption).Length == 0)
                        continue;
                }
                catch (IOException ex)
                {
                    Log.Warning(ex, "Could not enumerate plugin DLLs in directory {PluginDirectory}", dir);
                    continue;
                }

                existing = dir;
                break;
            }

            if (existing != null)
                chosen.Add(existing);
        }

        return chosen.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
