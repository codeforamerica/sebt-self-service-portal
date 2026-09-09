namespace SEBT.Portal.Tests.Integration.PluginIntegration;

/// <summary>
/// Resolves plugin directory paths relative to the solution root.
/// Walks up from the test assembly's base directory until it finds SEBT.Portal.sln,
/// then resolves plugin paths relative to the Api project.
/// </summary>
internal static class PluginPathResolver
{
    private const string SolutionFileName = "SEBT.Portal.sln";

    /// <summary>
    /// Returns the absolute path to a plugin directory (e.g., "plugins-dc")
    /// under src/SEBT.Portal.Api/.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when SEBT.Portal.sln cannot be found.</exception>
    public static string Resolve(string pluginDir)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, SolutionFileName)))
            {
                return Path.Combine(dir.FullName, "src", "SEBT.Portal.Api", pluginDir);
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find {SolutionFileName} by walking up from {AppContext.BaseDirectory}");
    }

    /// <summary>
    /// Returns true if the specified plugin directory exists and contains at least one DLL.
    /// Returns false (rather than throwing) if the solution root or directory can't be found.
    /// </summary>
    public static bool HasPluginDlls(string pluginDir)
    {
        try
        {
            var path = Resolve(pluginDir);
            return Directory.Exists(path) &&
                   Directory.GetFiles(path, "*.dll").Length > 0;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
