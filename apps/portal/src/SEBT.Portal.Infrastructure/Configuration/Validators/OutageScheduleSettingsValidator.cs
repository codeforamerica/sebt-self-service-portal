using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration.Validators;

/// <summary>
/// Rejects a malformed outage schedule at startup rather than letting the evaluator skip windows it
/// cannot parse. A mistyped date or target would otherwise degrade silently, and a window that
/// cannot be evaluated still makes the schedule authoritative for its surface, suppressing the
/// manual outage flag. Failing the host is the only signal loud enough.
/// <para>
/// Every problem is reported together so an operator fixes the whole schedule in one pass instead
/// of discovering the next bad window on the next failed boot.
/// </para>
/// </summary>
public sealed class OutageScheduleSettingsValidator : IValidateOptions<OutageScheduleSettings>
{
    private const string ExampleDateTime = "2026-06-21T22:00:00";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OutageScheduleSettings options)
    {
        var failures = new List<string>();

        ValidateTimeZone(options.TimeZoneId, failures);

        for (var i = 0; i < options.Windows.Count; i++)
        {
            ValidateWindow(i, options.Windows[i], failures);
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateTimeZone(string timeZoneId, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            failures.Add("OutageSchedule:TimeZoneId is required (e.g. 'America/Denver').");
            return;
        }

        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            failures.Add(
                $"OutageSchedule:TimeZoneId '{timeZoneId}' is not a known time zone "
                + "(e.g. 'America/Denver').");
        }
    }

    private static void ValidateWindow(int index, OutageWindow window, List<string> failures)
    {
        var startParsed = OutageWindowParsing.TryParseLocal(window.Start, out var start);
        if (!startParsed)
        {
            failures.Add(
                $"OutageSchedule:Windows[{index}]:Start '{window.Start}' is not a valid ISO-8601 "
                + $"local date-time (e.g. '{ExampleDateTime}').");
        }

        var endParsed = OutageWindowParsing.TryParseLocal(window.End, out var end);
        if (!endParsed)
        {
            failures.Add(
                $"OutageSchedule:Windows[{index}]:End '{window.End}' is not a valid ISO-8601 "
                + $"local date-time (e.g. '{ExampleDateTime}').");
        }

        // Windows are start-inclusive and end-exclusive, so an End equal to Start never activates.
        if (startParsed && endParsed && end <= start)
        {
            failures.Add(
                $"OutageSchedule:Windows[{index}]:End '{window.End}' must be after "
                + $"Start '{window.Start}'.");
        }

        if (!OutageWindowParsing.TryParseTarget(window.Target, out _))
        {
            failures.Add(
                $"OutageSchedule:Windows[{index}]:Target '{window.Target}' is not recognized; "
                + "expected Portal, EnrollmentChecker, or Both (omit for Both).");
        }
    }
}
