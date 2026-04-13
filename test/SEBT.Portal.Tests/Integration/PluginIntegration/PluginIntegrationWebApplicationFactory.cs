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
/// but does NOT register mock plugin stubs — real plugins or default fallbacks
/// provide the implementations via PluginLoader factory delegates.
/// </summary>
public class PluginIntegrationWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string? _pluginDir;
    private readonly List<string> _envKeysToClean = new();

    public PluginIntegrationWebApplicationFactory(
        string? pluginDir = null,
        Dictionary<string, string>? configOverrides = null)
    {
        _pluginDir = pluginDir;

        // Set config overrides as environment variables so they're visible
        // when Program.cs reads configuration during startup.
        if (configOverrides != null)
        {
            foreach (var (key, value) in configOverrides)
            {
                var envKey = key.Replace(":", "__");
                Environment.SetEnvironmentVariable(envKey, value);
                _envKeysToClean.Add(envKey);
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Override plugin assembly paths via environment variables BEFORE the server starts.
        // Environment variables are visible immediately when Program.cs reads configuration,
        // unlike AddInMemoryCollection which can be applied too late.
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__0",
            _pluginDir != null ? PluginPathResolver.Resolve(_pluginDir) : "plugins-none");
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__1", "plugins-none");
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey",
            "integration-test-key-must-be-at-least-32-bytes-long");
        Environment.SetEnvironmentVariable("MinimumIal__ApplicationCases", "IAL1");
        Environment.SetEnvironmentVariable("MinimumIal__CoLoadedStreamlineCases", "IAL1");
        Environment.SetEnvironmentVariable("MinimumIal__NonCoLoadedStreamlineCases", "IAL1plus");

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
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__0", null);
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__1", null);
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey", null);
        Environment.SetEnvironmentVariable("MinimumIal__ApplicationCases", null);
        Environment.SetEnvironmentVariable("MinimumIal__CoLoadedStreamlineCases", null);
        Environment.SetEnvironmentVariable("MinimumIal__NonCoLoadedStreamlineCases", null);
        foreach (var key in _envKeysToClean)
        {
            Environment.SetEnvironmentVariable(key, null);
        }

        base.Dispose(disposing);
    }
}
