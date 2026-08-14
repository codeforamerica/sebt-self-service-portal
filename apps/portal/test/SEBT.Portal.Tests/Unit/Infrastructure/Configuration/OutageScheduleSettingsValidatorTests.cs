using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration;
using SEBT.Portal.Infrastructure.Configuration.Validators;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Configuration;

public class OutageScheduleSettingsValidatorTests
{
    private readonly OutageScheduleSettingsValidator _validator = new();

    // The base appsettings.json ships no OutageSchedule section at all, so the app boots with the
    // defaults. If those ever fail validation, nothing starts — including every integration test.
    [Fact]
    public void Validate_DefaultSettings_ReturnsSuccess()
    {
        var result = _validator.Validate(null, new OutageScheduleSettings());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WellFormedWindows_ReturnsSuccess()
    {
        var settings = Settings(
            Window("2026-06-15T22:00:00", "2026-06-16T06:00:00", "Both"),
            Window("2026-06-20T22:00:00", "2026-06-23T06:00:00", "Portal"),
            Window("2026-06-25T22:00:00", "2026-06-26T06:00:00", "enrollmentchecker"));

        var result = _validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    // Windows are documented as optional; omitting Target means "Both".
    [Fact]
    public void Validate_WindowWithEmptyTarget_ReturnsSuccess()
    {
        var settings = Settings(Window("2026-06-15T22:00:00", "2026-06-16T06:00:00", string.Empty));

        var result = _validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    // The example appsettings files ship windows that have already elapsed. A past window is a
    // no-op, not a configuration error.
    [Fact]
    public void Validate_WindowEntirelyInThePast_ReturnsSuccess()
    {
        var settings = Settings(Window("2020-01-01T00:00:00", "2020-01-02T00:00:00", "Both"));

        var result = _validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_UnknownTimeZone_Fails()
    {
        var settings = Settings();
        settings.TimeZoneId = "Not/A_Real_Zone";

        var result = _validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("OutageSchedule:TimeZoneId") && f.Contains("Not/A_Real_Zone"));
    }

    [Fact]
    public void Validate_BlankTimeZone_Fails()
    {
        var settings = Settings();
        settings.TimeZoneId = "   ";

        var result = _validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("OutageSchedule:TimeZoneId is required"));
    }

    [Fact]
    public void Validate_UnparseableStart_FailsAndNamesTheWindowAndValue()
    {
        var settings = Settings(Window("2026-13-45T99:00", "2026-06-16T06:00:00", "Both"));

        var result = _validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Failures!,
            f => f.Contains("OutageSchedule:Windows[0]:Start") && f.Contains("2026-13-45T99:00"));
    }

    [Fact]
    public void Validate_UnparseableEnd_FailsAndNamesTheWindowAndValue()
    {
        var settings = Settings(Window("2026-06-15T22:00:00", "not-a-date", "Both"));

        var result = _validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Failures!,
            f => f.Contains("OutageSchedule:Windows[0]:End") && f.Contains("not-a-date"));
    }

    [Fact]
    public void Validate_EndBeforeStart_Fails()
    {
        var settings = Settings(Window("2026-06-16T06:00:00", "2026-06-15T22:00:00", "Both"));

        var result = _validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("OutageSchedule:Windows[0]:End") && f.Contains("must be after"));
    }

    // Start-inclusive, end-exclusive: a zero-length window can never be active, so it is a mistake.
    [Fact]
    public void Validate_EndEqualToStart_Fails()
    {
        var settings = Settings(Window("2026-06-15T22:00:00", "2026-06-15T22:00:00", "Both"));

        var result = _validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failures!, f => f.Contains("must be after"));
    }

    [Fact]
    public void Validate_UnrecognizedTarget_FailsAndListsTheAllowedValues()
    {
        var settings = Settings(Window("2026-06-15T22:00:00", "2026-06-16T06:00:00", "Prtal"));

        var result = _validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Contains(
            result.Failures!,
            f => f.Contains("OutageSchedule:Windows[0]:Target")
                && f.Contains("Prtal")
                && f.Contains("Portal, EnrollmentChecker, or Both"));
    }

    // One boot attempt should tell an operator everything that is wrong, not just the first thing.
    [Fact]
    public void Validate_SeveralProblems_ReportsAllOfThem()
    {
        var settings = Settings(
            Window("2026-13-45T99:00", "2026-06-16T06:00:00", "Both"),
            Window("2026-06-20T22:00:00", "2026-06-23T06:00:00", "Prtal"),
            Window("2026-06-26T06:00:00", "2026-06-25T22:00:00", "Portal"));
        settings.TimeZoneId = "Not/A_Real_Zone";

        var result = _validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        var failures = result.Failures!.ToList();
        Assert.Equal(4, failures.Count);
        Assert.Contains(failures, f => f.Contains("TimeZoneId"));
        Assert.Contains(failures, f => f.Contains("Windows[0]:Start"));
        Assert.Contains(failures, f => f.Contains("Windows[1]:Target"));
        Assert.Contains(failures, f => f.Contains("Windows[2]:End"));
    }

    private static OutageScheduleSettings Settings(params OutageWindow[] windows) =>
        new() { TimeZoneId = "America/Denver", Windows = windows.ToList() };

    private static OutageWindow Window(string start, string end, string target) =>
        new() { Start = start, End = end, Target = target };
}
