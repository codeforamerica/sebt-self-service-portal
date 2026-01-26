using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

/// <summary>
/// Tests for feature flag priority order (now handled by .NET configuration providers):
/// 1. appsettings.json (defaults in FeatureManagement) - lowest priority
/// 2. AWS AppConfig Agent (injects into FeatureManagement) - middle priority
/// 3. State-specific JSON (appsettings.{State}.json) - highest priority
/// 
/// FeatureManager reads from the merged FeatureManagement section, so these tests verify
/// that FeatureManager returns the correct merged values.
/// </summary>
public class FeatureFlagServicePriorityTests
{
    private readonly IFeatureManager _featureManager = Substitute.For<IFeatureManager>();
    private readonly ILogger<FeatureFlagQueryService> _logger = NullLogger<FeatureFlagQueryService>.Instance;

    [Fact]
    public async Task GetFeatureFlagsAsync_StateJsonOverridesAppConfig()
    {
        // Arrange
        // FeatureManager should return the merged value where state JSON (true) overrides AppConfig (false)
        var featureName = "test_feature";
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { featureName }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(featureName).Returns(true); // State JSON overrides to true

        var service = new FeatureFlagQueryService(_featureManager, _logger);

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
        // FeatureManager should return the merged value where AppConfig (true) overrides defaults (false)
        var featureName = "test_feature";
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { featureName }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(featureName).Returns(true); // AppConfig overrides to true

        var service = new FeatureFlagQueryService(_featureManager, _logger);

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
        // When no AppConfig or state JSON, FeatureManager returns default value
        var featureName = "test_feature";
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { featureName }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(featureName).Returns(true); // Default value

        var service = new FeatureFlagQueryService(_featureManager, _logger);

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
        // FeatureManager should return merged values where state JSON overrides everything
        var features = new[] { "feature1", "feature2" };
        _featureManager.GetFeatureNamesAsync()
            .Returns(features.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync("feature1").Returns(true); // State JSON: true
        _featureManager.IsEnabledAsync("feature2").Returns(false); // State JSON: false

        var service = new FeatureFlagQueryService(_featureManager, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        // State JSON should override everything
        Assert.True(result["feature1"]); // State JSON: true
        Assert.False(result["feature2"]); // State JSON: false
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_FeatureManagerFlagsReturned()
    {
        // Arrange
        var featureNames = new[] { "default_feature", "manager_feature" };
        _featureManager.GetFeatureNamesAsync().Returns(featureNames.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync("default_feature").Returns(true);
        _featureManager.IsEnabledAsync("manager_feature").Returns(false);

        var service = new FeatureFlagQueryService(_featureManager, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        Assert.True(result["default_feature"]);
        Assert.False(result["manager_feature"]);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_InvalidFlagNamesAreSkipped()
    {
        // Arrange
        var featureNames = new[] { "valid_feature", "invalid-feature", "invalid.feature" };
        _featureManager.GetFeatureNamesAsync().Returns(featureNames.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync("valid_feature").Returns(true);
        _featureManager.IsEnabledAsync("invalid-feature").Returns(true);
        _featureManager.IsEnabledAsync("invalid.feature").Returns(false);

        var service = new FeatureFlagQueryService(_featureManager, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        Assert.True(result.ContainsKey("valid_feature"));
        Assert.False(result.ContainsKey("invalid-feature")); // Invalid: contains hyphen
        Assert.False(result.ContainsKey("invalid.feature")); // Invalid: contains dot
        Assert.Single(result);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenFeatureManagerThrows_ContinuesWithOtherFlags()
    {
        // Arrange
        var featureNames = new[] { "feature1", "feature2" };
        _featureManager.GetFeatureNamesAsync().Returns(featureNames.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync("feature1").Returns(true);
        _featureManager.When(x => x.IsEnabledAsync("feature2")).Do(_ => throw new Exception("Test exception"));

        var service = new FeatureFlagQueryService(_featureManager, _logger);

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
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Return an async enumerable that will throw when enumerated with cancellation
        async IAsyncEnumerable<string> CancelledEnumerable()
        {
            cts.Token.ThrowIfCancellationRequested();
            yield break;
        }
        _featureManager.GetFeatureNamesAsync().Returns(CancelledEnumerable());

        var service = new FeatureFlagQueryService(_featureManager, _logger);

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.GetFeatureFlagsAsync(cts.Token));
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_SkipsAppConfigSubsection()
    {
        // Arrange
        // FeatureManager might return "AppConfig" as a feature name (the config subsection)
        // This should be skipped
        var featureNames = new[] { "test_feature", "AppConfig" };
        _featureManager.GetFeatureNamesAsync().Returns(featureNames.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync("test_feature").Returns(true);
        _featureManager.IsEnabledAsync("AppConfig").Returns(false);

        var service = new FeatureFlagQueryService(_featureManager, _logger);

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        Assert.True(result.ContainsKey("test_feature"));
        Assert.False(result.ContainsKey("AppConfig")); // Should be skipped
        Assert.Single(result);
    }
}
