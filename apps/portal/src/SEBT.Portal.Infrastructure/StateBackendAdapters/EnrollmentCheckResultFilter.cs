using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.Infrastructure.StateBackendAdapters;

/// <summary>
/// Flag-gated portal-side exact-match guard: drops Match/PossibleMatch candidates where neither
/// the DOB nor the full name exactly matches the submission — guards against CBMS fuzzy-match
/// false positives, regardless of the connector's confidence score.
/// </summary>
internal static class EnrollmentCheckResultFilter
{
    /// <summary>
    /// Removes any Match/PossibleMatch whose birth year differs, or where neither the full DOB nor
    /// the full name (case-insensitive) matches exactly. Error and NonMatch pass through; results
    /// whose CheckId has no corresponding request are kept (defensive).
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
