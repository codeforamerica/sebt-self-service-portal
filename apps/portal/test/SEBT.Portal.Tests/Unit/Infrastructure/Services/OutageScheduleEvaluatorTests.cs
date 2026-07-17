using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class OutageScheduleEvaluatorTests
{
    private static OutageScheduleEvaluator CreateEvaluator(OutageScheduleSettings settings, DateTimeOffset utcNow)
    {
        var monitor = Substitute.For<IOptionsMonitor<OutageScheduleSettings>>();
        monitor.CurrentValue.Returns(settings);
        var timeProvider = new FakeTimeProvider(utcNow);
        return new OutageScheduleEvaluator(monitor, timeProvider, NullLogger<OutageScheduleEvaluator>.Instance);
    }

    private static OutageScheduleSettings Settings(string timeZoneId, params (string start, string end)[] windows) =>
        new()
        {
            TimeZoneId = timeZoneId,
            Windows = windows.Select(w => new OutageWindow { Start = w.start, End = w.end }).ToList()
        };

    private static OutageScheduleSettings SettingsWithTargets(
        string timeZoneId,
        params (string start, string end, string target)[] windows) =>
        new()
        {
            TimeZoneId = timeZoneId,
            Windows = windows.Select(w => new OutageWindow { Start = w.start, End = w.end, Target = w.target }).ToList()
        };

    // A window active at this instant for tests that vary only the Target.
    private const string ActiveStart = "2026-06-21T00:00:00";
    private const string ActiveEnd = "2026-06-22T00:00:00";
    private static readonly DateTimeOffset InsideActiveWindowUtc = new(2026, 6, 21, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsOutageActive_WhenNowInsideWindow_ReturnsTrue()
    {
        // Window 2026-06-21 (Denver, MDT = UTC-6); now is noon local (18:00 UTC).
        var settings = Settings("America/Denver", ("2026-06-21T00:00:00", "2026-06-22T00:00:00"));
        var evaluator = CreateEvaluator(settings, new DateTimeOffset(2026, 6, 21, 18, 0, 0, TimeSpan.Zero));

        Assert.True(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void IsOutageActive_WhenNowBeforeWindow_ReturnsFalse()
    {
        var settings = Settings("America/Denver", ("2026-06-21T00:00:00", "2026-06-22T00:00:00"));
        // Day before the window.
        var evaluator = CreateEvaluator(settings, new DateTimeOffset(2026, 6, 20, 18, 0, 0, TimeSpan.Zero));

        Assert.False(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void IsOutageActive_WhenNowAfterWindow_ReturnsFalse()
    {
        var settings = Settings("America/Denver", ("2026-06-21T00:00:00", "2026-06-22T00:00:00"));
        // Day after the window.
        var evaluator = CreateEvaluator(settings, new DateTimeOffset(2026, 6, 23, 18, 0, 0, TimeSpan.Zero));

        Assert.False(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void IsOutageActive_WhenNoWindows_ReturnsFalse()
    {
        var settings = Settings("America/Denver");
        var evaluator = CreateEvaluator(settings, new DateTimeOffset(2026, 6, 21, 18, 0, 0, TimeSpan.Zero));

        Assert.False(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void IsOutageActive_DuringStandardTime_AppliesMinusSevenOffset()
    {
        // Winter window 02:00–03:00 local. Denver is MST (UTC-7) in January.
        // 09:30 UTC → 02:30 local (inside). With an incorrect -6 offset it would be 03:30 (outside),
        // so this proves standard-time conversion is applied.
        var settings = Settings("America/Denver", ("2026-01-15T02:00:00", "2026-01-15T03:00:00"));
        var evaluator = CreateEvaluator(settings, new DateTimeOffset(2026, 1, 15, 9, 30, 0, TimeSpan.Zero));

        Assert.True(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void IsOutageActive_DuringDaylightTime_AppliesMinusSixOffset()
    {
        // Summer window 02:00–03:00 local. Denver is MDT (UTC-6) in July.
        // 08:30 UTC → 02:30 local (inside). With an incorrect -7 offset it would be 01:30 (outside),
        // so this proves daylight-time conversion is applied.
        var settings = Settings("America/Denver", ("2026-07-15T02:00:00", "2026-07-15T03:00:00"));
        var evaluator = CreateEvaluator(settings, new DateTimeOffset(2026, 7, 15, 8, 30, 0, TimeSpan.Zero));

        Assert.True(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void IsOutageActive_AtWindowStart_ReturnsTrue_StartInclusive()
    {
        // Window starts 2026-06-21T00:00 local = 06:00 UTC (MDT). Exactly at start → active.
        var settings = Settings("America/Denver", ("2026-06-21T00:00:00", "2026-06-22T00:00:00"));
        var evaluator = CreateEvaluator(settings, new DateTimeOffset(2026, 6, 21, 6, 0, 0, TimeSpan.Zero));

        Assert.True(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void IsOutageActive_AtWindowEnd_ReturnsFalse_EndExclusive()
    {
        // Window ends 2026-06-22T00:00 local = 06:00 UTC (MDT). Exactly at end → not active.
        var settings = Settings("America/Denver", ("2026-06-21T00:00:00", "2026-06-22T00:00:00"));
        var evaluator = CreateEvaluator(settings, new DateTimeOffset(2026, 6, 22, 6, 0, 0, TimeSpan.Zero));

        Assert.False(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void IsOutageActive_WhenNowInsideSecondWindow_ReturnsTrue()
    {
        var settings = Settings(
            "America/Denver",
            ("2026-06-01T00:00:00", "2026-06-02T00:00:00"),
            ("2026-06-21T00:00:00", "2026-06-22T00:00:00"));
        var evaluator = CreateEvaluator(settings, new DateTimeOffset(2026, 6, 21, 18, 0, 0, TimeSpan.Zero));

        Assert.True(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void IsOutageActive_WhenWindowMalformed_SkipsItAndDoesNotThrow()
    {
        // A malformed window must be skipped; a valid active window later in the list still wins.
        var settings = Settings(
            "America/Denver",
            ("not-a-date", "also-not-a-date"),
            ("2026-06-21T00:00:00", "2026-06-22T00:00:00"));
        var evaluator = CreateEvaluator(settings, new DateTimeOffset(2026, 6, 21, 18, 0, 0, TimeSpan.Zero));

        Assert.True(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void IsOutageActive_WhenAllWindowsMalformed_ReturnsFalse()
    {
        var settings = Settings("America/Denver", ("not-a-date", "also-not-a-date"));
        var evaluator = CreateEvaluator(settings, new DateTimeOffset(2026, 6, 21, 18, 0, 0, TimeSpan.Zero));

        Assert.False(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void IsOutageActive_WhenTimeZoneInvalid_ReturnsFalse_AndDoesNotThrow()
    {
        var settings = Settings("Not/A_Real_Zone", ("2026-06-21T00:00:00", "2026-06-22T00:00:00"));
        var evaluator = CreateEvaluator(settings, new DateTimeOffset(2026, 6, 21, 18, 0, 0, TimeSpan.Zero));

        Assert.False(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void IsOutageActive_WhenWindowTargetsPortal_ActiveForPortalOnly()
    {
        var settings = SettingsWithTargets("America/Denver", (ActiveStart, ActiveEnd, "Portal"));
        var evaluator = CreateEvaluator(settings, InsideActiveWindowUtc);

        Assert.True(evaluator.IsOutageActive(OutageTarget.Portal));
        Assert.False(evaluator.IsOutageActive(OutageTarget.EnrollmentChecker));
    }

    [Fact]
    public void IsOutageActive_WhenWindowTargetsEnrollmentChecker_ActiveForCheckerOnly()
    {
        var settings = SettingsWithTargets("America/Denver", (ActiveStart, ActiveEnd, "EnrollmentChecker"));
        var evaluator = CreateEvaluator(settings, InsideActiveWindowUtc);

        Assert.False(evaluator.IsOutageActive(OutageTarget.Portal));
        Assert.True(evaluator.IsOutageActive(OutageTarget.EnrollmentChecker));
    }

    [Fact]
    public void IsOutageActive_WhenWindowTargetsBoth_ActiveForBothSurfaces()
    {
        var settings = SettingsWithTargets("America/Denver", (ActiveStart, ActiveEnd, "Both"));
        var evaluator = CreateEvaluator(settings, InsideActiveWindowUtc);

        Assert.True(evaluator.IsOutageActive(OutageTarget.Portal));
        Assert.True(evaluator.IsOutageActive(OutageTarget.EnrollmentChecker));
    }

    [Fact]
    public void IsOutageActive_WhenTargetOmitted_DefaultsToBoth()
    {
        // The plain Settings helper leaves Target at its default (empty string).
        // A scheduled backend outage takes down every surface unless the window says otherwise.
        var settings = Settings("America/Denver", (ActiveStart, ActiveEnd));
        var evaluator = CreateEvaluator(settings, InsideActiveWindowUtc);

        Assert.True(evaluator.IsOutageActive(OutageTarget.Portal));
        Assert.True(evaluator.IsOutageActive(OutageTarget.EnrollmentChecker));
    }

    [Fact]
    public void IsOutageActive_TargetParsingIsCaseInsensitive()
    {
        var settings = SettingsWithTargets("America/Denver", (ActiveStart, ActiveEnd, "enrollmentchecker"));
        var evaluator = CreateEvaluator(settings, InsideActiveWindowUtc);

        Assert.True(evaluator.IsOutageActive(OutageTarget.EnrollmentChecker));
        Assert.False(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void IsOutageActive_WhenTargetUnrecognized_SkipsWindowAndDoesNotThrow()
    {
        // A typo'd Target must not throw and must not activate any surface; a valid
        // window later in the list still wins (mirrors malformed Start/End handling).
        var settings = SettingsWithTargets(
            "America/Denver",
            (ActiveStart, ActiveEnd, "Prtal"),
            (ActiveStart, ActiveEnd, "EnrollmentChecker"));
        var evaluator = CreateEvaluator(settings, InsideActiveWindowUtc);

        Assert.False(evaluator.IsOutageActive(OutageTarget.Portal));
        Assert.True(evaluator.IsOutageActive(OutageTarget.EnrollmentChecker));
    }

    [Fact]
    public void IsOutageActive_WhenOnlyWindowHasUnrecognizedTarget_ReturnsFalseForAllSurfaces()
    {
        var settings = SettingsWithTargets("America/Denver", (ActiveStart, ActiveEnd, "Prtal"));
        var evaluator = CreateEvaluator(settings, InsideActiveWindowUtc);

        Assert.False(evaluator.IsOutageActive(OutageTarget.Portal));
        Assert.False(evaluator.IsOutageActive(OutageTarget.EnrollmentChecker));
    }

    [Fact]
    public void HasScheduledWindows_WhenWindowTargetsPortal_TrueForPortalOnly()
    {
        var settings = SettingsWithTargets("America/Denver", (ActiveStart, ActiveEnd, "Portal"));
        var evaluator = CreateEvaluator(settings, InsideActiveWindowUtc);

        Assert.True(evaluator.HasScheduledWindows(OutageTarget.Portal));
        Assert.False(evaluator.HasScheduledWindows(OutageTarget.EnrollmentChecker));
    }

    [Fact]
    public void HasScheduledWindows_WhenTargetOmitted_TrueForBothSurfaces()
    {
        var settings = Settings("America/Denver", (ActiveStart, ActiveEnd));
        var evaluator = CreateEvaluator(settings, InsideActiveWindowUtc);

        Assert.True(evaluator.HasScheduledWindows(OutageTarget.Portal));
        Assert.True(evaluator.HasScheduledWindows(OutageTarget.EnrollmentChecker));
    }

    [Fact]
    public void HasScheduledWindows_WhenDatesMalformedButTargetValid_False()
    {
        // A window IsOutageActive cannot evaluate must not make the schedule authoritative, or the
        // surface is pinned to "no outage" with its manual toggle suppressed. Startup validation
        // rejects such a window outright; this keeps the two methods agreeing if one ever slips by.
        var settings = SettingsWithTargets("America/Denver", ("not-a-date", "also-not-a-date", "Portal"));
        var evaluator = CreateEvaluator(settings, InsideActiveWindowUtc);

        Assert.False(evaluator.HasScheduledWindows(OutageTarget.Portal));
        Assert.False(evaluator.IsOutageActive(OutageTarget.Portal));
    }

    [Fact]
    public void HasScheduledWindows_WhenOneWindowMalformedAndAnotherValid_TrueForTheValidOne()
    {
        var settings = SettingsWithTargets(
            "America/Denver",
            ("not-a-date", "also-not-a-date", "Portal"),
            (ActiveStart, ActiveEnd, "EnrollmentChecker"));
        var evaluator = CreateEvaluator(settings, InsideActiveWindowUtc);

        Assert.False(evaluator.HasScheduledWindows(OutageTarget.Portal));
        Assert.True(evaluator.HasScheduledWindows(OutageTarget.EnrollmentChecker));
    }

    [Fact]
    public void HasScheduledWindows_WhenTargetUnrecognized_False()
    {
        // A window we skip for evaluation must not silently lock out the manual toggle.
        var settings = SettingsWithTargets("America/Denver", (ActiveStart, ActiveEnd, "Prtal"));
        var evaluator = CreateEvaluator(settings, InsideActiveWindowUtc);

        Assert.False(evaluator.HasScheduledWindows(OutageTarget.Portal));
        Assert.False(evaluator.HasScheduledWindows(OutageTarget.EnrollmentChecker));
    }

    [Fact]
    public void HasScheduledWindows_WhenNoWindows_False()
    {
        var settings = Settings("America/Denver");
        var evaluator = CreateEvaluator(settings, InsideActiveWindowUtc);

        Assert.False(evaluator.HasScheduledWindows(OutageTarget.Portal));
        Assert.False(evaluator.HasScheduledWindows(OutageTarget.EnrollmentChecker));
    }
}
