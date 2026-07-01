using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Evaluates the configured <see cref="OutageScheduleSettings"/> against the current time so the
/// outage page can auto-enable during scheduled (state-backend) maintenance windows without a
/// manual flag toggle. Reads the schedule via <see cref="IOptionsMonitor{TOptions}"/> so AWS
/// AppConfig updates take effect without a redeploy. Defensive by design: bad configuration
/// (unknown timezone, unparseable window) is logged and skipped rather than thrown, so the
/// feature-flags endpoint can never break.
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

    public bool IsOutageActive()
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

    /// <summary>
    /// Parses an ISO-8601 local wall-clock date-time (no offset) as <see cref="DateTimeKind.Unspecified"/>,
    /// matching the timezone-local <c>nowLocal</c> it is compared against.
    /// </summary>
    private static bool TryParseLocal(string value, out DateTime result) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
}
