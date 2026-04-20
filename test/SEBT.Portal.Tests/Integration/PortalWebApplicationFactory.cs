using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Shared test factory for integration tests that spin up the real HTTP pipeline.
/// Uses environment variables for configuration because WebApplicationFactory's
/// ConfigureAppConfiguration can trigger ConfigurationManager disposal races
/// when multiple IClassFixture test classes share a factory in the same collection.
/// Env vars are cleaned up in Dispose; the [Collection("Integration")] attribute
/// serializes test classes so there is no cross-test contamination.
/// </summary>
public class PortalWebApplicationFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// JWT signing key injected into the test host. Tests that mint their own
    /// tokens (e.g. AuthCookieAuthenticationTests) reference this constant so the
    /// signature matches what the JwtBearer middleware will validate against.
    /// </summary>
    public const string JwtSecretKey = "integration-test-secret-key-at-least-32-chars!";

    private static readonly string[] EnvVarKeys =
    [
        "PluginAssemblyPaths__0",
        "PluginAssemblyPaths__1",
        "JwtSettings__SecretKey",
        "STATE",
        "Oidc__DiscoveryEndpoint",
        "Oidc__ClientId",
        "Oidc__CallbackRedirectUri",
        "Oidc__CompleteLoginSigningKey",
        "ConnectionStrings__Redis",
        "MinimumIal__ApplicationCases",
        "MinimumIal__CoLoadedStreamlineCases",
        "MinimumIal__NonCoLoadedStreamlineCases"
    ];


    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Plugin paths — prevent loading DLLs with missing transitive dependencies
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__0", "plugins-none");
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__1", "plugins-none");

        // JWT + OIDC config for auth integration tests
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey", JwtSecretKey);
        Environment.SetEnvironmentVariable("STATE", "co");
        Environment.SetEnvironmentVariable("Oidc__DiscoveryEndpoint", "https://auth.example.com/.well-known/openid-configuration");
        Environment.SetEnvironmentVariable("Oidc__ClientId", "test-client");
        Environment.SetEnvironmentVariable("Oidc__CallbackRedirectUri", "http://localhost:3000/callback");
        Environment.SetEnvironmentVariable("Oidc__CompleteLoginSigningKey", JwtSecretKey);

        // Disable Redis so HybridCache uses in-memory only (no 5s timeout per op)
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "");

        // MinimumIal settings are required — app fails to start without them
        Environment.SetEnvironmentVariable("MinimumIal__ApplicationCases", "IAL1");
        Environment.SetEnvironmentVariable("MinimumIal__CoLoadedStreamlineCases", "IAL1");
        Environment.SetEnvironmentVariable("MinimumIal__NonCoLoadedStreamlineCases", "IAL1plus");

        builder.ConfigureServices(services =>
        {
            // Replace database services with no-op mocks so startup
            // doesn't require a real SQL Server instance.
            ReplaceWithMock<IDatabaseMigrator>(services);
            ReplaceWithMock<IDatabaseSeeder>(services);

            // Remove Redis-backed IDistributedCache if registered (belt-and-braces alongside
            // the empty connection string above).
            var redisDescriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache)
                && d.ImplementationType?.FullName?.Contains("Redis", StringComparison.OrdinalIgnoreCase) == true);
            if (redisDescriptor != null)
                services.Remove(redisDescriptor);
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
        foreach (var key in EnvVarKeys)
            Environment.SetEnvironmentVariable(key, null);
        base.Dispose(disposing);
    }
}
