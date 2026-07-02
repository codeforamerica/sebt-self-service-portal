using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Resolves whether a surface's outage page should currently be shown, combining the outage
/// schedule with the surface's manual feature flag. The rule, per surface: when any schedule
/// windows target the surface, the schedule is the sole authority (a manual "true" cannot bypass
/// the maintenance calendar, and a manual "false" cannot suppress a scheduled outage); with no
/// windows targeting the surface, the manual flag decides (the emergency path for unscheduled
/// outages).
/// </summary>
public interface IOutagePageStateResolver
{
    /// <summary>
    /// Returns true when the outage page should be shown for <paramref name="surface"/>.
    /// Pass a single surface (<see cref="OutageTarget.Portal"/> or
    /// <see cref="OutageTarget.EnrollmentChecker"/>); <see cref="OutageTarget.Both"/> is a
    /// window-level value and throws here.
    /// </summary>
    Task<bool> IsOutagePageActiveAsync(OutageTarget surface);
}
