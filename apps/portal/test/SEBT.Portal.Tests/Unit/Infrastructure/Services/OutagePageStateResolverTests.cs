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

        var result = await CreateResolver().ResolveAsync(surface);

        Assert.True(result.IsActive);
        Assert.True(result.ScheduleIsAuthority);
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

        var result = await CreateResolver().ResolveAsync(surface);

        Assert.False(result.IsActive);
        Assert.True(result.ScheduleIsAuthority);
    }

    [Fact]
    public async Task WhenNoWindowsTargetPortal_PortalManualFlagDecides()
    {
        _scheduleEvaluator.HasScheduledWindows(OutageTarget.Portal).Returns(false);
        _featureManager.IsEnabledAsync(FeatureFlags.OutagePageEnabled).Returns(true);

        var result = await CreateResolver().ResolveAsync(OutageTarget.Portal);

        Assert.True(result.IsActive);
        Assert.False(result.ScheduleIsAuthority);
        await _featureManager.Received(1).IsEnabledAsync(FeatureFlags.OutagePageEnabled);
    }

    [Fact]
    public async Task WhenNoWindowsTargetChecker_CheckerManualFlagDecides()
    {
        _scheduleEvaluator.HasScheduledWindows(OutageTarget.EnrollmentChecker).Returns(false);
        _featureManager.IsEnabledAsync(FeatureFlags.CheckerOutagePageEnabled).Returns(true);

        var result = await CreateResolver().ResolveAsync(OutageTarget.EnrollmentChecker);

        Assert.True(result.IsActive);
        Assert.False(result.ScheduleIsAuthority);
        await _featureManager.Received(1).IsEnabledAsync(FeatureFlags.CheckerOutagePageEnabled);
    }

    [Theory]
    [InlineData(OutageTarget.Portal)]
    [InlineData(OutageTarget.EnrollmentChecker)]
    public async Task WhenNoWindowsAndManualFlagOff_ReturnsFalse(OutageTarget surface)
    {
        _scheduleEvaluator.HasScheduledWindows(surface).Returns(false);
        _featureManager.IsEnabledAsync(Arg.Any<string>()).Returns(false);

        var result = await CreateResolver().ResolveAsync(surface);

        Assert.False(result.IsActive);
        Assert.False(result.ScheduleIsAuthority);
    }

    // Windows targeting one surface must leave the other surface's manual toggle reachable.
    [Fact]
    public async Task WhenWindowsTargetOnlyPortal_CheckerManualFlagStillDecides()
    {
        _scheduleEvaluator.HasScheduledWindows(OutageTarget.Portal).Returns(true);
        _scheduleEvaluator.IsOutageActive(OutageTarget.Portal).Returns(false);
        _scheduleEvaluator.HasScheduledWindows(OutageTarget.EnrollmentChecker).Returns(false);
        _featureManager.IsEnabledAsync(FeatureFlags.CheckerOutagePageEnabled).Returns(true);

        var result = await CreateResolver().ResolveAsync(OutageTarget.EnrollmentChecker);

        Assert.True(result.IsActive);
        Assert.False(result.ScheduleIsAuthority);
    }

    [Fact]
    public async Task WhenSurfaceIsBoth_Throws()
    {
        // Both is a window-level value, not a queryable surface.
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => CreateResolver().ResolveAsync(OutageTarget.Both));
    }
}
