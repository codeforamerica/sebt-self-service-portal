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
    private readonly IOutageScheduleEvaluator _outageScheduleEvaluator = Substitute.For<IOutageScheduleEvaluator>();
    private readonly IOptionsMonitor<OutageScheduleSettings> _outageScheduleOptions =
        Substitute.For<IOptionsMonitor<OutageScheduleSettings>>();
    private readonly ILogger<FeatureFlagQueryService> _logger = NullLogger<FeatureFlagQueryService>.Instance;

    public FeatureFlagServiceTests()
    {
        _outageScheduleOptions.CurrentValue.Returns(new OutageScheduleSettings());
    }

    // IsOutageActive() defaults to false (NSubstitute bool default), so existing tests are unaffected.
    private FeatureFlagQueryService CreateService() =>
        new(_featureManager, _outageScheduleEvaluator, _outageScheduleOptions, _logger);

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenFlagIsEnabled_ShouldReturnTrue()
    {
        // Arrange
        var featureName = "test_feature";
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { featureName }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(featureName).Returns(true);

        var service = CreateService();

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

        var service = CreateService();

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

        var service = CreateService();

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
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

        var service = CreateService();

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

        var service = CreateService();

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        Assert.Single(result);
        Assert.True(result.ContainsKey(configuredFeature));
        Assert.False(result.ContainsKey("unknown_feature"));
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenScheduledOutageActive_ShouldForceOutageFlagTrue()
    {
        // Arrange: outage flag manually disabled, but a scheduled outage window is active.
        _outageScheduleOptions.CurrentValue.Returns(new OutageScheduleSettings
        {
            Windows = [new OutageWindow { Start = "2026-06-21T00:00:00", End = "2026-06-22T00:00:00" }]
        });
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { FeatureFlags.OutagePageEnabled }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(FeatureFlags.OutagePageEnabled).Returns(false);
        _outageScheduleEvaluator.IsOutageActive().Returns(true);

        var service = CreateService();

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert: the schedule overrides the disabled manual value.
        Assert.True(result[FeatureFlags.OutagePageEnabled]);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenScheduledOutageActiveAndFlagNotConfigured_ShouldAddOutageFlagTrue()
    {
        // Arrange: no flags configured at all, but a scheduled outage window is active.
        _outageScheduleOptions.CurrentValue.Returns(new OutageScheduleSettings
        {
            Windows = [new OutageWindow { Start = "2026-06-21T00:00:00", End = "2026-06-22T00:00:00" }]
        });
        _featureManager.GetFeatureNamesAsync()
            .Returns(AsyncEnumerable.Empty<string>());
        _outageScheduleEvaluator.IsOutageActive().Returns(true);

        var service = CreateService();

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert: the flag is added even though FeatureManager never enumerated it.
        Assert.True(result[FeatureFlags.OutagePageEnabled]);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenScheduleConfiguredButInactive_ShouldForceOutageFlagFalse()
    {
        // Arrange: manual/AppConfig "true" must not bypass the maintenance calendar.
        _outageScheduleOptions.CurrentValue.Returns(new OutageScheduleSettings
        {
            Windows = [new OutageWindow { Start = "2026-06-21T00:00:00", End = "2026-06-22T00:00:00" }]
        });
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { FeatureFlags.OutagePageEnabled }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(FeatureFlags.OutagePageEnabled).Returns(true);
        _outageScheduleEvaluator.IsOutageActive().Returns(false);

        var service = CreateService();

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        Assert.False(result[FeatureFlags.OutagePageEnabled]);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenNoScheduledOutage_ShouldPreserveManualOutageFlag()
    {
        // Arrange: no OutageSchedule windows — manual/AppConfig toggle still works.
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { FeatureFlags.OutagePageEnabled }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(FeatureFlags.OutagePageEnabled).Returns(true);
        _outageScheduleEvaluator.IsOutageActive().Returns(false);

        var service = CreateService();

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert: the manual flag value is preserved (manual toggle still works).
        Assert.True(result[FeatureFlags.OutagePageEnabled]);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenScheduledOutageActive_ShouldNotAffectOtherFlags()
    {
        // Arrange
        _outageScheduleOptions.CurrentValue.Returns(new OutageScheduleSettings
        {
            Windows = [new OutageWindow { Start = "2026-06-21T00:00:00", End = "2026-06-22T00:00:00" }]
        });
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { "other_feature" }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync("other_feature").Returns(false);
        _outageScheduleEvaluator.IsOutageActive().Returns(true);

        var service = CreateService();

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert: only the outage flag is forced on; unrelated flags keep their values.
        Assert.False(result["other_feature"]);
        Assert.True(result[FeatureFlags.OutagePageEnabled]);
    }
}
