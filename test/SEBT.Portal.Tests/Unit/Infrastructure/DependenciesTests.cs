using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Infrastructure;

namespace SEBT.Portal.Tests.Unit.Infrastructure;

/// <summary>
/// Unit tests for <see cref="Dependencies"/> (service registration).
/// </summary>
public class DependenciesTests
{
    [Fact]
    public void ResolveIHouseholdRepository_WhenUseMockHouseholdDataFalseAndNoPlugin_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configData = new Dictionary<string, string?>
        {
            ["UseMockHouseholdData"] = "false"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddPortalInfrastructureRepositories(configuration);
        var provider = services.BuildServiceProvider();

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IHouseholdRepository>());
        Assert.Contains("UseMockHouseholdData is false", ex.Message);
        Assert.Contains("no household plugin", ex.Message);
    }

    [Fact]
    public void SmartyHttpClient_CanBeCreated_WithValidateScopesEnabled()
    {
        // Arrange: match the Development-mode DI setup (ValidateScopes=true) so the
        // HttpClient factory's configure delegate runs in the singleton root provider,
        // which cannot resolve scoped services like IOptionsSnapshot<T>.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions<SmartySettings>().Configure(s =>
        {
            s.Enabled = true;
            s.AuthId = "test-id";
            s.AuthToken = "test-token";
        });
        services.AddOptions<AddressValidationPolicySettings>();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Socure:Enabled"] = "false"
            })
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddPortalInfrastructureServices(configuration);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true });

        // Act: resolving from a scope mirrors the request-path behavior; creating the
        // "Smarty" client triggers the registered configure delegate.
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();

        // Assert: before the fix, this throws InvalidOperationException about
        // resolving IOptionsSnapshot<SmartySettings> from the root provider.
        var client = factory.CreateClient("Smarty");
        Assert.NotNull(client);
        Assert.NotNull(client.BaseAddress);
    }
}
