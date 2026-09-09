using SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck;

namespace SEBT.Portal.StatesPlugins.Interfaces;

/// <summary>
/// State plugin interface for checking child enrollment in Summer EBT benefits.
/// Each state provides its own implementation that delegates to state-specific
/// APIs or databases for enrollment matching.
/// </summary>
public interface IEnrollmentCheckService : IStatePlugin
{
    /// <summary>
    /// Checks enrollment status for one or more children.
    /// </summary>
    /// <param name="request">The enrollment check request containing children to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Results for each child checked.</returns>
    Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request,
        CancellationToken cancellationToken = default);
}
