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
}
