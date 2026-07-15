using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Evaluates the configured <see cref="OutageScheduleSettings"/> against the current time so the
/// outage page can auto-enable during scheduled (state-backend) maintenance windows without a
/// manual flag toggle. Windows apply per-surface via <see cref="OutageWindow.Target"/>. Reads the
/// schedule via <see cref="IOptionsMonitor{TOptions}"/> so AWS AppConfig updates take effect
/// without a redeploy.
/// <para>
/// Startup validation rejects an unknown timezone, an unparseable window, and an unrecognized
/// target, so none of those should reach this class. The checks below remain as a backstop and log
/// at Error: reaching one means validation was bypassed, and the feature-flag endpoints must keep
/// answering either way. Both methods share <see cref="OutageWindowParsing"/> with the validator, so
/// their notion of a well-formed window cannot drift apart.
/// </para>
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
            _logger.LogError(
                ex,
                "Invalid OutageSchedule TimeZoneId '{TimeZoneId}'; treating as no scheduled outage",
                settings.TimeZoneId);
            return false;
        }

        var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(_timeProvider.GetUtcNow().UtcDateTime, timeZone);

        foreach (var window in settings.Windows)
        {
            if (!OutageWindowParsing.TryParseTarget(window.Target, out var target))
            {
                _logger.LogError(
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

            if (!OutageWindowParsing.TryParseLocal(window.Start, out var start)
                || !OutageWindowParsing.TryParseLocal(window.End, out var end))
            {
                _logger.LogError(
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
            // A window only makes the schedule authoritative for a surface when it can actually be
            // evaluated. A window this method counted but IsOutageActive could not read would pin
            // the surface to "no outage" and suppress its manual flag, so the two must agree on
            // exactly which windows count. IsOutageActive logs the ones it rejects; logging again
            // here would double every message on every request.
            if (!OutageWindowParsing.TryParseTarget(window.Target, out var target)
                || !AppliesTo(target, surface))
            {
                continue;
            }

            if (OutageWindowParsing.TryParseLocal(window.Start, out _)
                && OutageWindowParsing.TryParseLocal(window.End, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static bool AppliesTo(OutageTarget windowTarget, OutageTarget surface) =>
        windowTarget == OutageTarget.Both || windowTarget == surface;
}
