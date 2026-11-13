using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Hosting;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Tests.Integration.Extensions;

namespace SEBT.Portal.Tests.Integration.StartupValidation;

public abstract class StartupValidationTestBase : IDisposable
{
    // Any name but Development, so the API's gitignored appsettings.Development.json and
    // user secrets never load. STATE stays unset for the same reason (appsettings.{state}.json).
    private const string TestingEnvironment = "Testing";

    private readonly HashSet<string> _touchedKeys = new(StringComparer.Ordinal);
    private readonly string _environmentName;

    protected StartupValidationTestBase(string environmentName = TestingEnvironment)
    {
        _environmentName = environmentName;

        var isProduction = _environmentName == Environments.Production;

        SetEnv("PluginAssemblyPaths__0", "plugins-none");
        SetEnv("PluginAssemblyPaths__1", "plugins-none");
        SetEnv("JwtSettings__SecretKey", "integration-test-jwt-secret-key-at-least-32-chars!");
        SetEnv("Oidc__DiscoveryEndpoint", "https://auth.example.com/.well-known/openid-configuration");
        SetEnv("Oidc__ClientId", "test-client");
        SetEnv("Oidc__CallbackRedirectUri", "http://localhost:3000/callback");
        SetEnv("Oidc__CompleteLoginSigningKey", "integration-test-secret-key-at-least-32-chars!");
        SetEnv("Oidc__VerificationClaims__LevelClaimName", "socureIdVerificationLevel");
        SetEnv("Oidc__VerificationClaims__DateClaimName", "socureIdVerificationDate");
        SetEnv("MinimumIal__ApplicationCases", "IAL1");
        SetEnv("MinimumIal__CoLoadedStreamlineCases", "IAL1");
        SetEnv("MinimumIal__NonCoLoadedStreamlineCases", "IAL1plus");

        // Neutralize startup rules outside the section under test, so only that section can fail:
        //  - Outside Development, Redis is required once OIDC is configured.
        //  - In Production, appsettings.json's IdentifierHasher placeholder is forbidden.
        SetEnv("ConnectionStrings__Redis", "localhost:6379");
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
            builder.UseEnvironment(_environmentName);

            builder.UseContentRoot(IsolatedContentRoot.Value);
            builder.ConfigureAppConfiguration((_, configuration) => RemoveLocalJsonSources(configuration));

            builder.ConfigureServices(services =>
            {
                services.ReplaceWithMock<IDatabaseMigrator>();
                services.ReplaceWithMock<IDatabaseSeeder>();
            });
        });

    /// <summary>
    /// A content root holding only the tracked appsettings.json. The API project folder and the test
    /// output both also hold developer-local overlays, and some settings are read while services are
    /// still registering, before <see cref="RemoveLocalJsonSources"/> runs.
    /// </summary>
    private static readonly Lazy<string> IsolatedContentRoot = new(() =>
    {
        var directory = Directory.CreateDirectory(
            Path.Combine(AppContext.BaseDirectory, "isolated-content-root")).FullName;
        File.Copy(
            Path.Combine(AppContext.BaseDirectory, "appsettings.json"),
            Path.Combine(directory, "appsettings.json"),
            overwrite: true);
        return directory;
    });

    /// <summary>
    /// Drops every JSON source except the tracked appsettings.json. Environment and state overlays,
    /// user secrets, and factory settings files exist only on developer machines, never in CI.
    /// </summary>
    private static void RemoveLocalJsonSources(IConfigurationBuilder configuration)
    {
        var localSources = configuration.Sources
            .OfType<JsonConfigurationSource>()
            .Where(source => source.Path != "appsettings.json")
            .ToList();

        foreach (var source in localSources)
        {
            configuration.Sources.Remove(source);
        }
    }

    public void Dispose()
    {
        foreach (var key in _touchedKeys)
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }
}
