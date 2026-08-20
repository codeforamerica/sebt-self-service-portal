using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Tests.Integration.Extensions;

namespace SEBT.Portal.Tests.Integration.StartupValidation;

public abstract class StartupValidationTestBase : IDisposable
{
    private readonly HashSet<string> _touchedKeys = new(StringComparer.Ordinal);
    private readonly string? _environmentName;

    protected StartupValidationTestBase(string? environmentName = null)
    {
        _environmentName = environmentName;

        var isProduction = _environmentName == Environments.Production;

        SetEnv("PluginAssemblyPaths__0", "plugins-none");
        SetEnv("PluginAssemblyPaths__1", "plugins-none");
        SetEnv("STATE", "co");
        SetEnv("JwtSettings__SecretKey", "integration-test-jwt-secret-key-at-least-32-chars!");
        SetEnv("Oidc__DiscoveryEndpoint", "https://auth.example.com/.well-known/openid-configuration");
        SetEnv("Oidc__ClientId", "test-client");
        SetEnv("Oidc__CallbackRedirectUri", "http://localhost:3000/callback");
        SetEnv("Oidc__CompleteLoginSigningKey", "integration-test-secret-key-at-least-32-chars!");
        SetEnv("MinimumIal__ApplicationCases", "IAL1");
        SetEnv("MinimumIal__CoLoadedStreamlineCases", "IAL1");
        SetEnv("MinimumIal__NonCoLoadedStreamlineCases", "IAL1plus");

        // Production has two rules that non-production environments don't:
        //  - Redis is required once OIDC is configured.
        //  - appsettings.json sets the forbidden IdentifierHasher placeholder, which the prod-only
        //    validator rejects; we need to set a valid key so only the section under test can fail.
        SetEnv("ConnectionStrings__Redis", isProduction ? "localhost:6379" : "");
        if (isProduction)
        {
            SetEnv("IdentifierHasher__SecretKey", "integration-test-identifier-hasher-key-32chars!");
        }
    }

    /// <summary>Sets an env var and remembers it, so Dispose clears it.</summary>
    protected void SetEnv(string key, string? value)
    {
        _touchedKeys.Add(key);
        Environment.SetEnvironmentVariable(key, value);
    }

    protected WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            if (_environmentName is not null)
            {
                builder.UseEnvironment(_environmentName);
            }

            builder.ConfigureServices(services =>
            {
                services.ReplaceWithMock<IDatabaseMigrator>();
                services.ReplaceWithMock<IDatabaseSeeder>();
            });
        });

    public void Dispose()
    {
        foreach (var key in _touchedKeys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}
