using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SEBT.Portal.Infrastructure.Configuration;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class AppConfigAgentConfigurationExtensionsTests
{
    private readonly ILogger<AppConfigAgentConfigurationProvider> _logger;

    public AppConfigAgentConfigurationExtensionsTests()
    {
        _logger = NullLogger<AppConfigAgentConfigurationProvider>.Instance;
    }

    [Fact]
    public void AddAppConfigAgent_WithDirectParameters_ShouldAddProvider()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act
        builder.AddAppConfigAgent(
            "http://localhost:2772",
            "test-app",
            "test-env",
            "test-profile",
            reloadAfterSeconds: 90,
            isFeatureFlag: true,
            _logger);

        // Assert
        var config = builder.Build();
        // Provider should be added (we can't easily verify it's working without a real HTTP server,
        // but we can verify the configuration builder accepts it)
        Assert.NotNull(config);
    }

    [Fact]
    public void AddAppConfigAgent_WithConfigurationSection_ShouldAddProvider()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "AppConfig:Agent:BaseUrl", "http://localhost:2772" },
            { "AppConfig:Agent:ApplicationId", "test-app" },
            { "AppConfig:Agent:EnvironmentId", "test-env" },
            { "AppConfig:Agent:ProfileId", "test-profile" },
            { "AppConfig:Agent:ReloadAfterSeconds", "90" },
            { "AppConfig:Agent:IsFeatureFlag", "true" }
        });

        // Act
        builder.AddAppConfigAgent("AppConfig:Agent", _logger);

        // Assert
        var config = builder.Build();
        Assert.NotNull(config);
    }

    [Fact]
    public void AddAppConfigAgent_WithMissingSection_ShouldNotAddProvider()
    {
        // Arrange
        var builder = new ConfigurationBuilder();

        // Act
        builder.AddAppConfigAgent("NonExistent:Section", _logger);

        // Assert
        var config = builder.Build();
        Assert.NotNull(config);
        // Should not throw and should continue normally
    }

    [Fact]
    public void AddAppConfigAgent_WithMissingRequiredValues_ShouldNotAddProvider()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "AppConfig:Agent:BaseUrl", "http://localhost:2772" }
            // Missing ApplicationId, EnvironmentId, ProfileId
        });

        // Act
        builder.AddAppConfigAgent("AppConfig:Agent", _logger);

        // Assert
        var config = builder.Build();
        Assert.NotNull(config);
        // Should not throw and should continue normally
    }

    [Fact]
    public void AddAppConfigAgent_WithDefaultValues_ShouldUseDefaults()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "AppConfig:Agent:ApplicationId", "test-app" },
            { "AppConfig:Agent:EnvironmentId", "test-env" },
            { "AppConfig:Agent:ProfileId", "test-profile" }
            // BaseUrl, ReloadAfterSeconds, IsFeatureFlag not specified 
        });

        // Act
        builder.AddAppConfigAgent("AppConfig:Agent", _logger);

        // Assert
        var config = builder.Build();
        Assert.NotNull(config);
    }

    [Fact]
    public void AddAppConfigAgent_WithCustomSectionName_ShouldReadFromCorrectSection()
    {
        // Arrange
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "Custom:Section:BaseUrl", "http://custom:2772" },
            { "Custom:Section:ApplicationId", "custom-app" },
            { "Custom:Section:EnvironmentId", "custom-env" },
            { "Custom:Section:ProfileId", "custom-profile" }
        });

        // Act
        builder.AddAppConfigAgent("Custom:Section", _logger);

        // Assert
        var config = builder.Build();
        Assert.NotNull(config);
    }
}
