using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Shared test factory for integration tests that spin up the real HTTP pipeline.
/// Handles common concerns so individual test classes can focus on endpoint behavior:
/// <list type="bullet">
///   <item>Redirects plugin assembly paths to prevent loading DLLs with missing transitive dependencies</item>
///   <item>Replaces database services with no-op mocks (no SQL Server required)</item>
/// </list>
/// </summary>
public class PortalWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Override plugin assembly paths via environment variables BEFORE the server starts.
        // WebApplicationFactory lazily starts the server, so env vars set here are visible
        // when Program.cs reads builder.Configuration during startup.
        // This prevents loading plugin DLLs (copied to test output by the API csproj)
        // that have unresolvable transitive dependencies in the test environment.
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__0", "plugins-none");
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__1", "plugins-none");

        // Provide a dummy JWT secret so the JwtBearer handler can initialize.
        // The auth middleware runs on every request (including /health), and
        // PostConfigure reads JwtSettings:SecretKey to create a SymmetricSecurityKey.
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey",
            "integration-test-secret-key-at-least-32-chars!");

        builder.ConfigureServices(services =>
        {
            // Replace database services with no-op mocks so startup
            // doesn't require a real SQL Server instance.
            ReplaceWithMock<IDatabaseMigrator>(services);
            ReplaceWithMock<IDatabaseSeeder>(services);
        });
    }

    /// <summary>
    /// Replaces an existing service registration with a no-op NSubstitute mock.
    /// </summary>
    private static void ReplaceWithMock<TService>(IServiceCollection services) where TService : class
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddScoped(_ => Substitute.For<TService>());
    }

    protected override void Dispose(bool disposing)
    {
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__0", null);
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__1", null);
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey", null);
        base.Dispose(disposing);
    }
}
