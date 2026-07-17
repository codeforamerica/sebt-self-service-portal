using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.UseCases.EnrollmentCheck;

/// <summary>
/// Portal-side filter for enrollment check results. When enabled via the
/// <c>enrollment_check_requires_at_least_one_exact_matched_field</c> feature flag, drops
/// Match/PossibleMatch candidates where neither the date of birth nor the full name
/// (first + last) is an exact match against the submitted request. This guards against
/// false positives from CBMS fuzzy-matching, regardless of the connector's confidence score.
/// </summary>
public static class EnrollmentCheckResultFilter
{
    /// <summary>
    /// Filters <paramref name="results"/> against the original <paramref name="requests"/>,
    /// removing any Match or PossibleMatch entry where the birth year does not match, or where
    /// the year matches but neither the full DOB nor the full name (first + last, case-insensitive)
    /// is an exact match. Error and NonMatch results pass through unchanged.
    /// Results whose CheckId has no corresponding request are kept (defensive).
    /// </summary>
    public static IList<ChildCheckResult> Filter(
        IList<ChildCheckRequest> requests,
        IList<ChildCheckResult> results)
    {
        var requestsByCheckId = requests.ToDictionary(r => r.CheckId);

        return results
            .Where(result => PassesFilter(result, requestsByCheckId))
            .ToList();
    }

    private static bool PassesFilter(
        ChildCheckResult result,
        Dictionary<Guid, ChildCheckRequest> requestsByCheckId)
    {
        if (result.Status is EnrollmentStatus.Error or EnrollmentStatus.NonMatch)
            return true;

        if (!requestsByCheckId.TryGetValue(result.CheckId, out var request))
            return true;

        if (result.DateOfBirth.Year != request.DateOfBirth.Year)
            return false;

        var dobMatches = result.DateOfBirth == request.DateOfBirth;
        var nameMatches =
            string.Equals(result.FirstName, request.FirstName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(result.LastName, request.LastName, StringComparison.OrdinalIgnoreCase);

        return dobMatches || nameMatches;
    }
}
