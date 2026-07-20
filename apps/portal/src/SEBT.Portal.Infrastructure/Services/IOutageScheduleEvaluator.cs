using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Decides whether the current time falls within a configured outage window so the outage page can
/// auto-enable during scheduled maintenance. Windows are evaluated per-surface via their
/// <see cref="OutageWindow.Target"/>.
/// </summary>
public interface IOutageScheduleEvaluator
{
    /// <summary>
    /// Returns true when "now" is inside any configured outage window that applies to
    /// <paramref name="surface"/> (its Target is that surface or "Both"). Never throws — missing or
    /// invalid configuration is treated as "no scheduled outage" (false).
    /// </summary>
    bool IsOutageActive(OutageTarget surface);

    /// <summary>
    /// Returns true when any configured window applies to <paramref name="surface"/>, regardless of
    /// whether its Start/End parse or whether it is currently active. Callers use this to decide
    /// whether the schedule (rather than a manual feature flag) is the authority for that surface.
    /// Windows with an unrecognized Target are excluded — a window skipped during evaluation must
    /// not silently lock out the manual toggle.
    /// </summary>
    bool HasScheduledWindows(OutageTarget surface);
}
