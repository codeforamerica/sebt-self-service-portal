using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Evaluates the configured <see cref="OutageScheduleSettings"/> against the current time so the
/// outage page can auto-enable during scheduled (state-backend) maintenance windows without a
/// manual flag toggle. Windows apply per-surface via <see cref="OutageWindow.Target"/>. Reads the
/// schedule via <see cref="IOptionsMonitor{TOptions}"/> so AWS AppConfig updates take effect
/// without a redeploy. Defensive by design: bad configuration (unknown timezone, unparseable
/// window, unrecognized target) is logged and skipped rather than thrown, so the feature-flags
/// endpoints can never break.
/// </summary>
public sealed class OutageScheduleEvaluator : IOutageScheduleEvaluator
{
    private readonly IOptionsMonitor<OutageScheduleSettings> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OutageScheduleEvaluator> _logger;

    public OutageScheduleEvaluator(
        IOptionsMonitor<OutageScheduleSettings> options,
        TimeProvider timeProvider,
        ILogger<OutageScheduleEvaluator> logger)
    {
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public bool IsOutageActive(OutageTarget surface)
    {
        var settings = _options.CurrentValue;
        if (settings.Windows.Count == 0)
        {
            return false;
        }

        TimeZoneInfo timeZone;
        try
        {
            timeZone = TimeZoneInfo.FindSystemTimeZoneById(settings.TimeZoneId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Invalid OutageSchedule TimeZoneId '{TimeZoneId}'; treating as no scheduled outage",
                settings.TimeZoneId);
            return false;
        }

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, timeZone);

        foreach (var window in settings.Windows)
        {
            if (!TryParseTarget(window.Target, out var target))
            {
                _logger.LogWarning(
                    "Skipping OutageSchedule window with unrecognized Target '{Target}' " +
                    "(Start='{Start}' End='{End}'); expected Portal, EnrollmentChecker, or Both",
                    window.Target,
                    window.Start,
                    window.End);
                continue;
            }

            if (!AppliesTo(target, surface))
            {
                continue;
            }

            if (!TryParseLocal(window.Start, out var start) || !TryParseLocal(window.End, out var end))
            {
                _logger.LogWarning(
                    "Skipping malformed OutageSchedule window Start='{Start}' End='{End}'",
                    window.Start,
                    window.End);
                continue;
            }

            // Start-inclusive, end-exclusive.
            if (start <= nowLocal && nowLocal < end)
            {
                return true;
            }
        }

        return false;
    }

    public bool HasScheduledWindows(OutageTarget surface)
    {
        foreach (var window in _options.CurrentValue.Windows)
        {
            // Unrecognized targets are excluded (IsOutageActive logs them; logging here too
            // would double the warning on every request). Malformed dates still count: a window
            // scheduled for a surface signals "the schedule is the authority for that surface"
            // even when its dates fail to parse.
            if (TryParseTarget(window.Target, out var target) && AppliesTo(target, surface))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AppliesTo(OutageTarget windowTarget, OutageTarget surface) =>
        windowTarget == OutageTarget.Both || windowTarget == surface;

    /// <summary>
    /// Parses a window's Target string case-insensitively. Empty or whitespace means "Both" —
    /// a scheduled backend outage affects every surface unless the window says otherwise.
    /// </summary>
    private static bool TryParseTarget(string value, out OutageTarget target)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            target = OutageTarget.Both;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out target) && Enum.IsDefined(target);
    }

    /// <summary>
    /// Parses an ISO-8601 local wall-clock date-time (no offset) as <see cref="DateTimeKind.Unspecified"/>,
    /// matching the timezone-local <c>nowLocal</c> it is compared against.
    /// </summary>
    private static bool TryParseLocal(string value, out DateTime result) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
}
