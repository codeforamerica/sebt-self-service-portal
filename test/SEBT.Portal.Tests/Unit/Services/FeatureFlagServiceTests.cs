using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class FeatureFlagServiceTests
{
    private readonly IFeatureManager _featureManager = Substitute.For<IFeatureManager>();
    private readonly ILogger<FeatureFlagQueryService> _logger = NullLogger<FeatureFlagQueryService>.Instance;

    private IOptions<FeatureManagementSettings> CreateEmptyFeatureManagementOptions()
    {
        return Options.Create(new FeatureManagementSettings());
    }

    private IOptions<AppConfigFeatureFlagSettings> CreateEmptyAppConfigOptions()
    {
        return Options.Create(new AppConfigFeatureFlagSettings());
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenFlagIsEnabled_ShouldReturnTrue()
    {
        // Arrange
        var featureName = "test_feature";
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { featureName }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(featureName).Returns(true);

        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var featureManagementOptions = CreateEmptyFeatureManagementOptions();
        var appConfigOptions = CreateEmptyAppConfigOptions();
        var service = new FeatureFlagQueryService(_featureManager, defaultFlagsOptions, featureManagementOptions, appConfigOptions, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        Assert.True(result.ContainsKey(featureName));
        Assert.True(result[featureName]);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenFlagIsDisabled_ShouldReturnFalse()
    {
        // Arrange
        var featureName = "test_feature";
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { featureName }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(featureName).Returns(false);

        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var featureManagementOptions = CreateEmptyFeatureManagementOptions();
        var appConfigOptions = CreateEmptyAppConfigOptions();
        var service = new FeatureFlagQueryService(_featureManager, defaultFlagsOptions, featureManagementOptions, appConfigOptions, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        Assert.True(result.ContainsKey(featureName));
        Assert.False(result[featureName]);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenNoFlagsConfigured_ShouldReturnEmptyDictionary()
    {
        // Arrange
        _featureManager.GetFeatureNamesAsync()
            .Returns(AsyncEnumerable.Empty<string>());

        // Empty default flags - no flags configured anywhere
        var emptyDefaultFlags = new DefaultFeatureFlagSettings { Flags = new Dictionary<string, bool>() };
        var emptyDefaultFlagsOptions = Options.Create(emptyDefaultFlags);
        var featureManagementOptions = CreateEmptyFeatureManagementOptions();
        var appConfigOptions = CreateEmptyAppConfigOptions();
        var service = new FeatureFlagQueryService(_featureManager, emptyDefaultFlagsOptions, featureManagementOptions, appConfigOptions, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        // Should return empty when no defaults, no state JSON, no AppConfig, and no FeatureManager flags
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenMultipleFlagsConfigured_ShouldReturnAllFlags()
    {
        // Arrange
        var features = new[] { "feature1", "feature2", "feature3" };
        _featureManager.GetFeatureNamesAsync()
            .Returns(features.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync("feature1").Returns(true);
        _featureManager.IsEnabledAsync("feature2").Returns(false);
        _featureManager.IsEnabledAsync("feature3").Returns(true);

        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var featureManagementOptions = CreateEmptyFeatureManagementOptions();
        var appConfigOptions = CreateEmptyAppConfigOptions();
        var service = new FeatureFlagQueryService(_featureManager, defaultFlagsOptions, featureManagementOptions, appConfigOptions, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.True(result["feature1"]);
        Assert.False(result["feature2"]);
        Assert.True(result["feature3"]);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenUnknownFlagNotConfigured_ShouldNotIncludeInResponse()
    {
        // Arrange
        var configuredFeature = "configured_feature";
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { configuredFeature }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(configuredFeature).Returns(true);

        var defaultFlags = new DefaultFeatureFlagSettings();
        var defaultFlagsOptions = Options.Create(defaultFlags);
        var featureManagementOptions = CreateEmptyFeatureManagementOptions();
        var appConfigOptions = CreateEmptyAppConfigOptions();
        var service = new FeatureFlagQueryService(_featureManager, defaultFlagsOptions, featureManagementOptions, appConfigOptions, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        Assert.Single(result);
        Assert.True(result.ContainsKey(configuredFeature));
        Assert.False(result.ContainsKey("unknown_feature"));
    }
}
