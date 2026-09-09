using System.Globalization;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Parses the string-typed fields of an <see cref="OutageWindow"/>. Shared by the startup validator
/// that rejects malformed schedules and by the evaluator that reads them at request time, so the two
/// cannot disagree about which windows are well-formed.
/// </summary>
public static class OutageWindowParsing
{
    /// <summary>
    /// Parses a window's Target string case-insensitively. Empty or whitespace means
    /// <see cref="OutageTarget.Both"/> — a scheduled backend outage affects every surface unless the
    /// window says otherwise.
    /// </summary>
    public static bool TryParseTarget(string value, out OutageTarget target)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            target = OutageTarget.Both;
            return true;
        }

        return Enum.TryParse(value, ignoreCase: true, out target) && Enum.IsDefined(target);
    }

    /// <summary>
    /// Parses an ISO-8601 local wall-clock date-time (no offset) as
    /// <see cref="DateTimeKind.Unspecified"/>, matching the timezone-local "now" it is compared
    /// against.
    /// </summary>
    public static bool TryParseLocal(string value, out DateTime result) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
}
