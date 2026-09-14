using Microsoft.Extensions.Options;

namespace SEBT.Portal.Tests.Integration.StartupValidation;

/// <summary>
/// Proves the app refuses to start when an outage window is malformed. Before ValidateOnStart, a
/// mistyped date or target was logged and skipped at request time, so a broken schedule could be
/// deployed and would quietly disable the outage page it was meant to schedule.
/// <para>
/// That the well-formed and empty cases still boot is covered by every other integration test in
/// this project: the base appsettings.json has no OutageSchedule section, so they all start the
/// host against the validated defaults.
/// </para>
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class OutageScheduleStartupValidationTests : StartupValidationTestBase
{
    // Program.cs adds appsettings.{state}.json after the environment-variable provider, so a state
    // file wins over anything set here. A high window index sidesteps that: a state file's windows
    // live at 0..n and cannot overwrite this one. Assertions match on the offending value rather
    // than its position in the bound list, which shifts when a state file contributes windows too.
    //
    // TimeZoneId has no such escape hatch — a state file that sets it would silently replace the
    // value under test — so the timezone rule is covered by OutageScheduleSettingsValidatorTests.
    private const string BadWindow = "9";

    [Fact]
    public void Startup_WithUnparseableWindowDates_ThrowsOptionsValidationException()
    {
        SetWindow(start: "2026-13-45T99:00", end: "not-a-date", target: "Both");

        using var factory = CreateFactory();
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("OutageSchedule", ex.Message);
        Assert.Contains("2026-13-45T99:00", ex.Message);
        Assert.Contains("not-a-date", ex.Message);
    }

    [Fact]
    public void Startup_WithUnrecognizedWindowTarget_ThrowsOptionsValidationException()
    {
        SetWindow(start: "2026-06-15T22:00:00", end: "2026-06-16T06:00:00", target: "Prtal");

        using var factory = CreateFactory();
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("Target", ex.Message);
        Assert.Contains("Prtal", ex.Message);
    }

    [Fact]
    public void Startup_WithEndBeforeStart_ThrowsOptionsValidationException()
    {
        SetWindow(start: "2026-06-16T06:00:00", end: "2026-06-15T22:00:00", target: "Both");

        using var factory = CreateFactory();
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("must be after", ex.Message);
    }

    private void SetWindow(string start, string end, string target)
    {
        SetEnv($"OutageSchedule__Windows__{BadWindow}__Start", start);
        SetEnv($"OutageSchedule__Windows__{BadWindow}__End", end);
        SetEnv($"OutageSchedule__Windows__{BadWindow}__Target", target);
    }
}
