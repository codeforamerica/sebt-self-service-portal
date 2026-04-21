using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
    public void CreateSmartyHttpClient_CanBeCreatedFromScope_WhenSmartyEnabled()
    {
        // Arrange — build a real DI container with Smarty enabled and scope
        // validation on, mimicking how ASP.NET Core validates service lifetimes.
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Smarty:Enabled"] = "true",
                ["Smarty:AuthId"] = "test-id",
                ["Smarty:AuthToken"] = "test-token",
                ["Smarty:BaseUrl"] = "https://us-street.api.smartystreets.com",
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddPortalInfrastructureAppSettings(config);
        services.AddPortalInfrastructureServices(config);

        var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true
        });

        // Act — creating the named HttpClient triggers the configuration delegate
        // which must resolve options from the root provider.
        using var scope = provider.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient("Smarty");

        // Assert
        Assert.NotNull(client);
        Assert.Equal(new Uri("https://us-street.api.smartystreets.com/"), client.BaseAddress);
    }
}
