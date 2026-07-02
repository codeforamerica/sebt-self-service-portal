namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configurable schedule of outage windows. When the current time (interpreted in
/// <see cref="TimeZoneId"/>) falls within any window, the portal force-enables the
/// <c>outage_page_enabled</c> feature flag so the outage page appears automatically — no manual
/// toggling required. Bind from appsettings.{State}.json or the AWS AppConfig AppSettings profile
/// (the latter allows updating windows at runtime without a redeploy).
/// </summary>
public sealed class OutageScheduleSettings
{
    public const string SectionName = "OutageSchedule";

    /// <summary>
    /// IANA timezone used to interpret each window's local Start/End (e.g. "America/Denver").
    /// </summary>
    public string TimeZoneId { get; set; } = "America/Denver";

    /// <summary>
    /// Scheduled outage windows. Empty (the default) means no scheduled outages.
    /// </summary>
    public List<OutageWindow> Windows { get; set; } = new();
}

/// <summary>
/// A single scheduled outage window. <see cref="Start"/> and <see cref="End"/> are local wall-clock
/// date-times in the schedule's <see cref="OutageScheduleSettings.TimeZoneId"/> (ISO-8601 with no
/// offset, e.g. "2026-06-21T22:00:00"). The window is start-inclusive and end-exclusive.
/// </summary>
public sealed class OutageWindow
{
    public string Start { get; set; } = string.Empty;

    public string End { get; set; } = string.Empty;

    /// <summary>
    /// Which surface(s) this window applies to: "Portal", "EnrollmentChecker", or "Both"
    /// (case-insensitive). Empty or missing means "Both" — a scheduled backend outage takes
    /// down the shared data source, so every surface is affected unless the window says
    /// otherwise. Kept as a string rather than <see cref="OutageTarget"/> so a typo in
    /// hand-edited configuration cannot fail options binding; the evaluator parses it
    /// defensively and skips windows it cannot understand.
    /// </summary>
    public string Target { get; set; } = string.Empty;
}

/// <summary>
/// Surfaces an outage window can apply to. Window <see cref="OutageWindow.Target"/> strings
/// parse to these values. When querying outage state, pass the asking surface
/// (<see cref="Portal"/> or <see cref="EnrollmentChecker"/>); <see cref="Both"/> is a
/// window-level value meaning the window applies to every surface.
/// </summary>
public enum OutageTarget
{
    Portal,
    EnrollmentChecker,
    Both
}
