using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Proves the app fails to start in Production when IdentifierHasher:SecretKey is a forbidden
/// placeholder. The prod-only IdentifierHasherSettingsValidator (which replaced IdentifierHasherGuard)
/// runs via ValidateOnStart and rejects the repo's dev placeholders. Outside Production the validator
/// skips, so this is Production-only behavior — the skip path is covered by the unit tests.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class IdentifierHasherSettingsStartupValidationTests : IDisposable
{
    private static readonly string[] EnvVarKeys =
    [
        "PluginAssemblyPaths__0",
        "PluginAssemblyPaths__1",
        "JwtSettings__SecretKey",
        "IdentifierHasher__SecretKey",
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

    public IdentifierHasherSettingsStartupValidationTests()
    {
        // Valid config for everything EXCEPT IdentifierHasher__SecretKey (set per-test), so the
        // only startup failure comes from the IdentifierHasher validator.
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__0", "plugins-none");
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__1", "plugins-none");
        Environment.SetEnvironmentVariable("STATE", "co");
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey",
            "integration-test-secret-key-at-least-32-chars!");
        Environment.SetEnvironmentVariable("Oidc__DiscoveryEndpoint",
            "https://auth.example.com/.well-known/openid-configuration");
        Environment.SetEnvironmentVariable("Oidc__ClientId", "test-client");
        Environment.SetEnvironmentVariable("Oidc__CallbackRedirectUri", "http://localhost:3000/callback");
        Environment.SetEnvironmentVariable("Oidc__CompleteLoginSigningKey",
            "integration-test-secret-key-at-least-32-chars!");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "localhost:6379");
        Environment.SetEnvironmentVariable("MinimumIal__ApplicationCases", "IAL1");
        Environment.SetEnvironmentVariable("MinimumIal__CoLoadedStreamlineCases", "IAL1");
        Environment.SetEnvironmentVariable("MinimumIal__NonCoLoadedStreamlineCases", "IAL1plus");
    }

    [Fact]
    public void Startup_InProduction_WithForbiddenPlaceholderKey_ThrowsOptionsValidationException()
    {
        // A >=32-char placeholder: passes the [MinLength(32)] DataAnnotation, so ONLY the prod-only
        // IdentifierHasher validator can reject it — this proves the validator is actually wired.
        Environment.SetEnvironmentVariable("IdentifierHasher__SecretKey",
            "OverrideInProductionUseEnvVarIDENTIFIERHASHER__SECRETKEY");

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment(Environments.Production);
                builder.ConfigureServices(services =>
                {
                    ReplaceWithMock<IDatabaseMigrator>(services);
                    ReplaceWithMock<IDatabaseSeeder>(services);
                });
            });

        // ValidateOnStart triggers during host startup — CreateClient() surfaces the failure.
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("IdentifierHasher", ex.Message);
    }

    private static void ReplaceWithMock<TService>(IServiceCollection services) where TService : class
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(TService));
        if (descriptor != null)
        {
            services.Remove(descriptor);
        }

        services.AddScoped(_ => Substitute.For<TService>());
    }

    public void Dispose()
    {
        foreach (var key in EnvVarKeys)
            Environment.SetEnvironmentVariable(key, null);
    }
}
