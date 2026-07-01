namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Decides whether the current time falls within a configured outage window so the outage page can
/// auto-enable during scheduled maintenance.
/// </summary>
public interface IOutageScheduleEvaluator
{
    /// <summary>
    /// Returns true when "now" is inside any configured outage window. Never throws — missing or
    /// invalid configuration is treated as "no scheduled outage" (false).
    /// </summary>
    bool IsOutageActive();
}
