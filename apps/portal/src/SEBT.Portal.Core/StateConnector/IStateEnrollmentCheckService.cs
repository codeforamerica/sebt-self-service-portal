namespace SEBT.Portal.Core.StateConnector;

/// <summary>
/// Portal port for checking child enrollment in Summer EBT benefits through the
/// loaded state connector plugin. Adapters in Infrastructure map between these
/// Core models and the plugin contract at the boundary.
/// </summary>
public interface IStateEnrollmentCheckService
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
