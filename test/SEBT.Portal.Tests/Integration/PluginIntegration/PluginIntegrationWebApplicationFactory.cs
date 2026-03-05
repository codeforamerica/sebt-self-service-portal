using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Tests.Integration.PluginIntegration;

/// <summary>
/// WebApplicationFactory for plugin integration tests.
/// Loads real MEF plugins from specified directories instead of registering mocks.
/// Uses InMemory EF and mock migrator/seeder (same as PortalWebApplicationFactory)
/// but does NOT register mock plugin stubs — real plugins or DefaultEnrollmentCheckService
/// provide the implementations.
/// </summary>
/// <remarks>
/// Environment variables must be set in the constructor because plugin loading
/// (AddPlugins) runs in Program.cs inline code before ConfigureWebHost callbacks fire.
/// All env vars are saved and restored on Dispose to avoid cross-test contamination.
/// </remarks>
public class PluginIntegrationWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _originalEnvVars = new();

    public PluginIntegrationWebApplicationFactory(
        string? pluginDir = null,
        Dictionary<string, string>? configOverrides = null)
    {
        // Resolve plugin path to an absolute path that WithAssembliesInPath can use.
        // Path.Combine(AppContext.BaseDirectory, absolutePath) returns absolutePath
        // because Path.Combine ignores the first arg when the second is absolute.
        if (pluginDir != null)
        {
            SetEnvVar("PluginAssemblyPaths__0", PluginPathResolver.Resolve(pluginDir));
        }
        else
        {
            // Point at a non-existent directory so AddPlugins doesn't throw
            // on missing config. WithAssembliesInPath silently skips it.
            SetEnvVar("PluginAssemblyPaths__0", "plugins-none");
        }

        SetEnvVar("JwtSettings__SecretKey",
            "integration-test-key-must-be-at-least-32-bytes-long");

        // Apply config overrides as env vars (e.g., DCConnector__ConnectionString).
        // The __ separator maps to : in .NET's configuration system.
        if (configOverrides != null)
        {
            foreach (var (key, value) in configOverrides)
            {
                SetEnvVar(key.Replace(":", "__"), value);
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Replace SQL Server with InMemory EF
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PortalDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            services.AddDbContext<PortalDbContext>(options =>
                options.UseInMemoryDatabase($"PluginIntegrationTests-{Guid.NewGuid()}"));

            // Replace database migrator and seeder with no-ops
            var migratorDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDatabaseMigrator));
            if (migratorDescriptor != null)
            {
                services.Remove(migratorDescriptor);
            }

            services.AddScoped(_ => Substitute.For<IDatabaseMigrator>());

            var seederDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IDatabaseSeeder));
            if (seederDescriptor != null)
            {
                services.Remove(seederDescriptor);
            }

            services.AddScoped(_ => Substitute.For<IDatabaseSeeder>());

            // ISummerEbtCaseService has no default fallback in AddPlugins, and
            // HouseholdRepository depends on it. Register a mock so DI validation
            // passes. TryAddSingleton is a no-op if a real plugin already registered it.
            services.TryAddSingleton(Substitute.For<ISummerEbtCaseService>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            // Restore all original env var values to avoid cross-test contamination
            foreach (var (key, originalValue) in _originalEnvVars)
            {
                Environment.SetEnvironmentVariable(key, originalValue);
            }
        }
    }

    /// <summary>
    /// Saves the current value and sets the new one.
    /// </summary>
    private void SetEnvVar(string key, string? value)
    {
        if (!_originalEnvVars.ContainsKey(key))
        {
            _originalEnvVars[key] = Environment.GetEnvironmentVariable(key);
        }

        Environment.SetEnvironmentVariable(key, value);
    }
}
