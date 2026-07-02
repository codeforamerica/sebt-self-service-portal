using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class FeatureFlagServiceTests
{
    private readonly IFeatureManager _featureManager = Substitute.For<IFeatureManager>();
    private readonly IOutageScheduleEvaluator _outageScheduleEvaluator = Substitute.For<IOutageScheduleEvaluator>();
    private readonly ILogger<FeatureFlagQueryService> _logger = NullLogger<FeatureFlagQueryService>.Instance;

    // Evaluator methods default to false (NSubstitute bool default), so tests not
    // arranging outage state behave as "no windows configured, no active outage".
    private FeatureFlagQueryService CreateService() =>
        new(_featureManager, _outageScheduleEvaluator, _logger);

    private void ArrangePortalWindows(bool active)
    {
        _outageScheduleEvaluator.HasScheduledWindows(OutageTarget.Portal).Returns(true);
        _outageScheduleEvaluator.IsOutageActive(OutageTarget.Portal).Returns(active);
    }

    private void ArrangeCheckerWindows(bool active)
    {
        _outageScheduleEvaluator.HasScheduledWindows(OutageTarget.EnrollmentChecker).Returns(true);
        _outageScheduleEvaluator.IsOutageActive(OutageTarget.EnrollmentChecker).Returns(active);
    }

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
        ArrangePortalWindows(active: true);
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { FeatureFlags.OutagePageEnabled }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(FeatureFlags.OutagePageEnabled).Returns(false);

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
        ArrangePortalWindows(active: true);
        _featureManager.GetFeatureNamesAsync()
            .Returns(AsyncEnumerable.Empty<string>());

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
        ArrangePortalWindows(active: false);
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { FeatureFlags.OutagePageEnabled }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(FeatureFlags.OutagePageEnabled).Returns(true);

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
        ArrangePortalWindows(active: true);
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { "other_feature" }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync("other_feature").Returns(false);

        var service = CreateService();

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert: only the outage flag is forced on; unrelated flags keep their values.
        Assert.False(result["other_feature"]);
        Assert.True(result[FeatureFlags.OutagePageEnabled]);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenOnlyCheckerWindowsExist_ShouldPreservePortalManualFlag()
    {
        // Arrange: windows targeting only the enrollment checker must not disable the
        // portal's manual toggle (the emergency kill switch for unscheduled outages).
        ArrangeCheckerWindows(active: true);
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { FeatureFlags.OutagePageEnabled }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(FeatureFlags.OutagePageEnabled).Returns(true);

        var service = CreateService();

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert: portal manual value preserved; checker flag forced on by its schedule.
        Assert.True(result[FeatureFlags.OutagePageEnabled]);
        Assert.True(result[FeatureFlags.CheckerOutagePageEnabled]);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenOnlyPortalWindowsExist_ShouldPreserveCheckerManualFlag()
    {
        // Arrange: the mirror case — portal windows must not disable the checker's manual toggle.
        ArrangePortalWindows(active: false);
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { FeatureFlags.CheckerOutagePageEnabled }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(FeatureFlags.CheckerOutagePageEnabled).Returns(true);

        var service = CreateService();

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert: checker manual value preserved; portal flag forced off by its inactive schedule.
        Assert.True(result[FeatureFlags.CheckerOutagePageEnabled]);
        Assert.False(result[FeatureFlags.OutagePageEnabled]);
    }

    [Fact]
    public async Task GetFeatureFlagsAsync_WhenCheckerScheduleInactive_ShouldForceCheckerFlagFalse()
    {
        // Arrange: checker windows exist but none is active; manual "true" cannot bypass.
        ArrangeCheckerWindows(active: false);
        _featureManager.GetFeatureNamesAsync()
            .Returns(new[] { FeatureFlags.CheckerOutagePageEnabled }.ToAsyncEnumerable());
        _featureManager.IsEnabledAsync(FeatureFlags.CheckerOutagePageEnabled).Returns(true);

        var service = CreateService();

        // Act
        var result = await service.GetFeatureFlagsAsync();

        // Assert
        Assert.False(result[FeatureFlags.CheckerOutagePageEnabled]);
    }
}
