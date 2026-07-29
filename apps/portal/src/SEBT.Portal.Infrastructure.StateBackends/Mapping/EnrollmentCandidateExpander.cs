namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>The closed <c>transposeMonthDay</c> DOB candidate-expansion strategy.</summary>
internal static class EnrollmentCandidateExpander
{
    /// <summary>
    /// Returns the month/day-swapped DOB, or <c>null</c> when the swap yields the same date or an
    /// invalid one. The swap is only valid when the day is a valid month (1-12); the original month
    /// is always a valid day, so no day-range check is needed.
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
