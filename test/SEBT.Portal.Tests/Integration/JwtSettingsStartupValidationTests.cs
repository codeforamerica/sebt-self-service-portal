using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Integration;

/// <summary>
/// Proves the app fails to start when JwtSettings.SecretKey is missing or too short.
/// DC-313: Without ValidateDataAnnotations() on the JwtSettings registration,
/// the [Required] and [MinLength(32)] attributes are not enforced and the app
/// happily starts with an empty signing key — a critical security vulnerability.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class JwtSettingsStartupValidationTests : IDisposable
{
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

    public JwtSettingsStartupValidationTests()
    {
        // Set all required config EXCEPT JwtSettings__SecretKey
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__0", "plugins-none");
        Environment.SetEnvironmentVariable("PluginAssemblyPaths__1", "plugins-none");
        Environment.SetEnvironmentVariable("STATE", "co");
        Environment.SetEnvironmentVariable("Oidc__DiscoveryEndpoint", "https://auth.example.com/.well-known/openid-configuration");
        Environment.SetEnvironmentVariable("Oidc__ClientId", "test-client");
        Environment.SetEnvironmentVariable("Oidc__CallbackRedirectUri", "http://localhost:3000/callback");
        Environment.SetEnvironmentVariable("Oidc__CompleteLoginSigningKey",
            "integration-test-secret-key-at-least-32-chars!");
        Environment.SetEnvironmentVariable("ConnectionStrings__Redis", "");
        Environment.SetEnvironmentVariable("MinimumIal__ApplicationCases", "IAL1");
        Environment.SetEnvironmentVariable("MinimumIal__CoLoadedStreamlineCases", "IAL1");
        Environment.SetEnvironmentVariable("MinimumIal__NonCoLoadedStreamlineCases", "IAL1plus");
    }

    [Fact]
    public void Startup_WithEmptyJwtSecretKey_ThrowsOptionsValidationException()
    {
        // appsettings.json has SecretKey: "" and we deliberately don't override it
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey", "");

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    ReplaceWithMock<IDatabaseMigrator>(services);
                    ReplaceWithMock<IDatabaseSeeder>(services);
                });
            });

        // ValidateOnStart triggers during host startup — CreateClient() surfaces the failure
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("SecretKey", ex.Message);
    }

    [Fact]
    public void Startup_WithTooShortJwtSecretKey_ThrowsOptionsValidationException()
    {
        Environment.SetEnvironmentVariable("JwtSettings__SecretKey", "too-short");

        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    ReplaceWithMock<IDatabaseMigrator>(services);
                    ReplaceWithMock<IDatabaseSeeder>(services);
                });
            });

        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("SecretKey", ex.Message);
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
