using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.Api.Composition.Defaults;

/// <summary>
/// Default implementation when no state-specific IEnrollmentCheckService plugin is loaded.
/// Returns NonMatch for every child — a conservative fallback that avoids false positives.
/// </summary>
internal class DefaultEnrollmentCheckService : IEnrollmentCheckService
{
    public Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new EnrollmentCheckResult
        {
            Results = request.Children.Select(c => new ChildCheckResult
            {
                CheckId = c.CheckId,
                FirstName = c.FirstName,
                LastName = c.LastName,
                DateOfBirth = c.DateOfBirth,
                Status = EnrollmentStatus.NonMatch
                // SchoolName is not echoed — no result through the lean enrollment path carries one.
            }).ToList(),
            ResponseMessage = "No enrollment check service configured."
        });
    }
}
