using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
/// <remarks>
/// Plugin paths and connection strings are injected via ConfigureAppConfiguration.
/// PluginLoader reads IConfiguration at DI resolution time, so it sees these
/// overrides without needing process-global environment variables.
/// </remarks>
public class PluginIntegrationWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string? _pluginDir;
    private readonly Dictionary<string, string>? _configOverrides;

    public PluginIntegrationWebApplicationFactory(
        string? pluginDir = null,
        Dictionary<string, string>? configOverrides = null)
    {
        _pluginDir = pluginDir;
        _configOverrides = configOverrides;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["PluginAssemblyPaths:0"] = _pluginDir != null
                    ? PluginPathResolver.Resolve(_pluginDir)
                    : "plugins-none",
                ["JwtSettings:SecretKey"] =
                    "integration-test-key-must-be-at-least-32-bytes-long",
            };

            if (_configOverrides != null)
            {
                foreach (var (key, value) in _configOverrides)
                {
                    overrides[key] = value;
                }
            }

            config.AddInMemoryCollection(overrides);
        });

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
}
