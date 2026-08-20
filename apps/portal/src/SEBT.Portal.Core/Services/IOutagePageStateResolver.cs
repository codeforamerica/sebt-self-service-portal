using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Whether a surface's outage page should currently be shown, and why.
/// </summary>
/// <param name="IsActive">True when the outage page should be shown for the surface.</param>
/// <param name="ScheduleIsAuthority">
/// True when schedule windows target the surface, meaning the schedule alone decided
/// <paramref name="IsActive"/> and the surface's manual feature flag was not consulted. Callers that
/// publish the manual flag's value need this to know whether to overwrite it with
/// <paramref name="IsActive"/>.
/// </param>
public readonly record struct OutagePageState(bool IsActive, bool ScheduleIsAuthority);

/// <summary>
/// The single authority on outage page state. Combines the outage schedule with the surface's manual
/// feature flag. The rule, per surface: when any schedule windows target the surface, the schedule
/// alone decides (a manual "true" cannot bypass the maintenance calendar, and a manual "false"
/// cannot suppress a scheduled outage); with no windows targeting the surface, the manual flag
/// decides (the emergency path for unscheduled outages).
/// <para>
/// Windows targeting one surface never affect the other surface's manual toggle.
/// </para>
/// </summary>
public interface IOutagePageStateResolver
{
    /// <summary>
    /// Resolves outage page state for <paramref name="surface"/>. Pass a single surface
    /// (<see cref="OutageTarget.Portal"/> or <see cref="OutageTarget.EnrollmentChecker"/>);
    /// <see cref="OutageTarget.Both"/> is a window-level value and throws here.
    /// </summary>
    Task<OutagePageState> ResolveAsync(OutageTarget surface);
}
