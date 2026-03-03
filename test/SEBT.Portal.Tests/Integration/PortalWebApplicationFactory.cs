using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
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
    public PortalWebApplicationFactory()
    {
        // These environment variables must be available *before* the host builder
        // runs inline code in Program.cs. Environment variables are the earliest
        // configuration source — ConfigureWebHost callbacks fire too late.
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__0", "plugins-test");

        // JWT auth middleware requires a non-empty secret key even for AllowAnonymous endpoints,
        // because the middleware always runs (it authenticates but doesn't require auth).
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey",
            "integration-test-key-must-be-at-least-32-bytes-long");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

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

            // Register mock stubs for plugin interfaces that other services depend on.
            // Without plugins loaded, these are missing and DI validation fails.
            services.AddSingleton(Substitute.For<ISummerEbtCaseService>());
            services.AddSingleton(Substitute.For<IEnrollmentCheckService>());
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            Environment.SetEnvironmentVariable("PluginAssemblyPaths__0", null);
            Environment.SetEnvironmentVariable("JwtSettings__SecretKey", null);
        }
    }
}
