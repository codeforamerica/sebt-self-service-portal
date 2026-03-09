using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Data;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.StatesPlugins.Interfaces;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Replaces SQL Server with InMemory EF provider and mocks
/// database migration/seeding so tests don't need a real database.
/// Plugin directories are configured but empty, so no state plugins load.
/// </summary>
public class PortalWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PluginAssemblyPaths:0"] = "plugins-test",
                ["JwtSettings:SecretKey"] =
                    "integration-test-key-must-be-at-least-32-bytes-long",
            }));

        builder.ConfigureServices(services =>
        {
            // Remove the real SQL Server DbContext registration
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<PortalDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Add InMemory EF provider instead
            services.AddDbContext<PortalDbContext>(options =>
                options.UseInMemoryDatabase("IntegrationTests"));

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

            // Override plugin factory delegates with mocks.
            // These AddSingleton calls come after AddPlugins' factory registrations
            // (which ran during Program.cs), so they win — last registration wins in DI.
            services.AddSingleton(Substitute.For<ISummerEbtCaseService>());
            services.AddSingleton(Substitute.For<IEnrollmentCheckService>());
        });
    }
}
