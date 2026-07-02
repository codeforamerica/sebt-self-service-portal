using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.FeatureManagement;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class OutagePageStateResolverTests
{
    private readonly IOutageScheduleEvaluator _scheduleEvaluator = Substitute.For<IOutageScheduleEvaluator>();
    private readonly IFeatureManager _featureManager = Substitute.For<IFeatureManager>();

    private OutagePageStateResolver CreateResolver() =>
        new(_scheduleEvaluator, _featureManager, NullLogger<OutagePageStateResolver>.Instance);

    [Theory]
    [InlineData(OutageTarget.Portal)]
    [InlineData(OutageTarget.EnrollmentChecker)]
    public async Task WhenWindowsTargetSurface_ScheduleActive_ReturnsTrue_WithoutConsultingManualFlag(
        OutageTarget surface)
    {
        _scheduleEvaluator.HasScheduledWindows(surface).Returns(true);
        _scheduleEvaluator.IsOutageActive(surface).Returns(true);

        var result = await CreateResolver().IsOutagePageActiveAsync(surface);

        Assert.True(result);
        await _featureManager.DidNotReceiveWithAnyArgs().IsEnabledAsync(default!);
    }

    [Theory]
    [InlineData(OutageTarget.Portal)]
    [InlineData(OutageTarget.EnrollmentChecker)]
    public async Task WhenWindowsTargetSurface_ScheduleInactive_ManualTrueCannotBypass(OutageTarget surface)
    {
        // A manual/AppConfig "true" must not bypass the maintenance calendar.
        _scheduleEvaluator.HasScheduledWindows(surface).Returns(true);
        _scheduleEvaluator.IsOutageActive(surface).Returns(false);
        _featureManager.IsEnabledAsync(Arg.Any<string>()).Returns(true);

        var result = await CreateResolver().IsOutagePageActiveAsync(surface);

        Assert.False(result);
    }

    [Fact]
    public async Task WhenNoWindowsTargetPortal_PortalManualFlagDecides()
    {
        _scheduleEvaluator.HasScheduledWindows(OutageTarget.Portal).Returns(false);
        _featureManager.IsEnabledAsync(FeatureFlags.OutagePageEnabled).Returns(true);

        var result = await CreateResolver().IsOutagePageActiveAsync(OutageTarget.Portal);

        Assert.True(result);
        await _featureManager.Received(1).IsEnabledAsync(FeatureFlags.OutagePageEnabled);
    }

    [Fact]
    public async Task WhenNoWindowsTargetChecker_CheckerManualFlagDecides()
    {
        _scheduleEvaluator.HasScheduledWindows(OutageTarget.EnrollmentChecker).Returns(false);
        _featureManager.IsEnabledAsync(FeatureFlags.CheckerOutagePageEnabled).Returns(true);

        var result = await CreateResolver().IsOutagePageActiveAsync(OutageTarget.EnrollmentChecker);

        Assert.True(result);
        await _featureManager.Received(1).IsEnabledAsync(FeatureFlags.CheckerOutagePageEnabled);
    }

    [Theory]
    [InlineData(OutageTarget.Portal)]
    [InlineData(OutageTarget.EnrollmentChecker)]
    public async Task WhenNoWindowsAndManualFlagOff_ReturnsFalse(OutageTarget surface)
    {
        _scheduleEvaluator.HasScheduledWindows(surface).Returns(false);
        _featureManager.IsEnabledAsync(Arg.Any<string>()).Returns(false);

        var result = await CreateResolver().IsOutagePageActiveAsync(surface);

        Assert.False(result);
    }

    [Fact]
    public async Task WhenSurfaceIsBoth_Throws()
    {
        // Both is a window-level value, not a queryable surface.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => CreateResolver().IsOutagePageActiveAsync(OutageTarget.Both));
    }
}
