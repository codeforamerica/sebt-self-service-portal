using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using NSubstitute;
using NSubstitute.Core;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

/// <summary>
/// Tests for feature flag priority order:
/// 1. State-specific JSON files (highest priority)
/// 2. AWS AppConfig
/// 3. Default feature flags (lowest priority)
/// 4. FeatureManager flags (for flags not in above sources)
/// </summary>
public class FeatureFlagServicePriorityTests
{
    private readonly IFeatureManager _featureManager = Substitute.For<IFeatureManager>();
    private readonly ILogger<FeatureFlagQueryService> _logger = NullLogger<FeatureFlagQueryService>.Instance;

    [Fact]
    public async Task GetFeatureFlagsAsync_StateJsonOverridesAppConfig()
    {
        // Arrange
        var configuration = CreateConfigurationWithStateJsonAndAppConfig();
        var defaultFlags = new DefaultFeatureFlagSettings
        {
            Flags = new Dictionary<string, bool> { { "test_feature", false } }
        };
        var defaultFlagsOptions = Options.Create(defaultFlags);
        _featureManager.GetFeatureNamesAsync().Returns(AsyncEnumerable.Empty<string>());

        var service = new FeatureFlagQueryService(_featureManager, configuration, defaultFlagsOptions, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        // State JSON should override AppConfig
        Assert.True(result["test_feature"]); // State JSON has true
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_AppConfigOverridesDefaults()
    {
        // Arrange
        var configuration = CreateConfigurationWithAppConfig();
        var defaultFlags = new DefaultFeatureFlagSettings
        {
            Flags = new Dictionary<string, bool> { { "test_feature", false } }
        };
        var defaultFlagsOptions = Options.Create(defaultFlags);
        _featureManager.GetFeatureNamesAsync().Returns(AsyncEnumerable.Empty<string>());

        var service = new FeatureFlagQueryService(_featureManager, configuration, defaultFlagsOptions, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        // AppConfig should override defaults
        Assert.True(result["test_feature"]); // AppConfig has true
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_FallsBackToDefaults()
    {
        // Arrange
        var configBuilder = new ConfigurationBuilder();
        var configuration = configBuilder.Build(); // Empty configuration

        var defaultFlags = new DefaultFeatureFlagSettings
        {
            Flags = new Dictionary<string, bool> { { "test_feature", true } }
        };
        var defaultFlagsOptions = Options.Create(defaultFlags);
        _featureManager.GetFeatureNamesAsync().Returns(AsyncEnumerable.Empty<string>());

        var service = new FeatureFlagQueryService(_featureManager, configuration, defaultFlagsOptions, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        // Should use defaults when no other source is configured
        Assert.True(result["test_feature"]);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_StateJsonHasHighestPriority()
    {
        // Arrange
        var configuration = CreateConfigurationWithAllSources();
        var defaultFlags = new DefaultFeatureFlagSettings
        {
            Flags = new Dictionary<string, bool>
            {
                { "feature1", false }, // Default: false
                { "feature2", false }  // Default: false
            }
        };
        var defaultFlagsOptions = Options.Create(defaultFlags);
        _featureManager.GetFeatureNamesAsync().Returns(AsyncEnumerable.Empty<string>());

        var service = new FeatureFlagQueryService(_featureManager, configuration, defaultFlagsOptions, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        // State JSON should override everything
        Assert.True(result["feature1"]); // State JSON: true
        Assert.False(result["feature2"]); // State JSON: false
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_FeatureManagerFlagsAddedWhenNotInOtherSources()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var defaultFlags = new DefaultFeatureFlagSettings
        {
            Flags = new Dictionary<string, bool> { { "default_feature", true } }
        };
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var featureNames = new[] { "default_feature", "manager_feature" };
        _featureManager.GetFeatureNamesAsync().Returns(featureNames.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync("default_feature").Returns(true);
        _featureManager.IsEnabledAsync("manager_feature").Returns(false);

        var service = new FeatureFlagQueryService(_featureManager, configuration, defaultFlagsOptions, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        Assert.True(result["default_feature"]); // From defaults
        Assert.False(result["manager_feature"]); // From FeatureManager
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_InvalidFlagNamesAreSkipped()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var defaultFlags = new DefaultFeatureFlagSettings
        {
            Flags = new Dictionary<string, bool>
            {
                { "valid_feature", true },
                { "invalid-feature", true }, // Invalid: contains hyphen
                { "invalid.feature", false } // Invalid: contains dot
            }
        };
        var defaultFlagsOptions = Options.Create(defaultFlags);
        _featureManager.GetFeatureNamesAsync().Returns(AsyncEnumerable.Empty<string>());

        var service = new FeatureFlagQueryService(_featureManager, configuration, defaultFlagsOptions, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        Assert.True(result.ContainsKey("valid_feature"));
        Assert.False(result.ContainsKey("invalid-feature"));
        Assert.False(result.ContainsKey("invalid.feature"));
        Assert.Single(result);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenFeatureManagerThrows_ContinuesWithOtherFlags()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var featureNames = new[] { "feature1", "feature2" };
        _featureManager.GetFeatureNamesAsync().Returns(featureNames.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync("feature1").Returns(true);
        _featureManager.When(x => x.IsEnabledAsync("feature2")).Do(_ => throw new Exception("Test exception"));

        var service = new FeatureFlagQueryService(_featureManager, configuration, defaultFlagsOptions, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        Assert.True(result.ContainsKey("feature1"));
        Assert.False(result.ContainsKey("feature2")); // Should be skipped due to exception
        Assert.Single(result);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_RespectsCancellationToken()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Return an async enumerable that will throw when enumerated with cancellation
        async IAsyncEnumerable<string> CancelledEnumerable()
        {
            cts.Token.ThrowIfCancellationRequested();
            yield break;
        }
        _featureManager.GetFeatureNamesAsync().Returns(CancelledEnumerable());

        var service = new FeatureFlagQueryService(_featureManager, configuration, defaultFlagsOptions, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GetFeatureFlagsAsync(cts.Token));
    }

    private IConfiguration CreateConfigurationWithStateJsonAndAppConfig()
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "FeatureManagement:test_feature", "true" }, // State JSON has true
            { "FeatureManagement:AppConfig:Enabled", "true" },
            { "FeatureManagement:AppConfig:Features:test_feature", "false" } // AppConfig has false
        });
        return configBuilder.Build();
    }

    private IConfiguration CreateConfigurationWithAppConfig()
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "FeatureManagement:AppConfig:Enabled", "true" },
            { "FeatureManagement:AppConfig:Features:test_feature", "true" } // AppConfig has true
        });
        return configBuilder.Build();
    }

    private IConfiguration CreateConfigurationWithAllSources()
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            { "FeatureManagement:feature1", "true" }, // State JSON: true
            { "FeatureManagement:feature2", "false" }, // State JSON: false
            { "FeatureManagement:AppConfig:Enabled", "true" }
            // No AppConfig features - should fall back to defaults
        });
        return configBuilder.Build();
    }
}
