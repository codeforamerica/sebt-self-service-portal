namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Request-side candidate expansion for the enrollment op (DC-568 spike): the closed
/// <c>transposeMonthDay</c> DOB strategy. A named brick, NOT a date-mangling mini-language.
/// </summary>
internal static class EnrollmentCandidateExpander
{
    /// <summary>
    /// Returns the month/day-swapped DOB when the swap yields a <em>different</em> valid calendar
    /// date, otherwise <c>null</c>. The swap is only valid when the day can also serve as a month
    /// (1-12); the original month (always 1-12) is in turn always a valid day, so no day-range check
    /// is needed. When month equals day the swap is a no-op and we return <c>null</c> so no duplicate
    /// candidate is emitted. Mirrors the Colorado connector's <c>TryTransposeMonthAndDay</c> exactly.
    /// </summary>
    public static DateOnly? TryTransposeMonthDay(DateOnly dob)
    {
        if (dob.Day > 12)
        {
            return null;
        }

        var transposed = new DateOnly(dob.Year, dob.Day, dob.Month);
        return transposed == dob ? null : transposed;
    }
}
