using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

namespace SEBT.Portal.Api.Startup;

/// <summary>
/// Adds the state-specific configuration overlay (appsettings.{state}.json)
/// to the application's configuration chain.
/// </summary>
public static class StateOverlayConfiguration
{
    /// <summary>
    /// Inserts appsettings.{state}.json into the configuration chain so that it
    /// overrides the base appsettings files but is itself overridden by
    /// environment variables and command-line args (twelve-factor precedence).
    /// </summary>
    /// <param name="configuration">The application's configuration manager.</param>
    /// <param name="state">The state code from the STATE environment variable (e.g., "dc", "co").</param>
    public static void AddStateOverlay(this ConfigurationManager configuration, string state)
    {
        var overlaySource = new JsonConfigurationSource
        {
            Path = $"appsettings.{state.ToLowerInvariant()}.json",
            Optional = true,
            ReloadOnChange = true,
        };

        // WebApplication.CreateBuilder has already registered the environment
        // variable and command-line providers, and later providers win. Appending
        // the overlay would let state JSON silently override values operators set
        // via env vars, so insert it just below the app-level environment variable
        // source instead. That source is the unprefixed one: the chain also
        // contains prefixed host sources (ASPNETCORE_/DOTNET_) that sit before the
        // appsettings files and must stay below the overlay.
        var environmentVariablesIndex = configuration.Sources.ToList().FindIndex(
            source => source is EnvironmentVariablesConfigurationSource { Prefix: null or "" });

        if (environmentVariablesIndex >= 0)
        {
            configuration.Sources.Insert(environmentVariablesIndex, overlaySource);
        }
        else
        {
            configuration.Sources.Add(overlaySource);
        }
    }
}
