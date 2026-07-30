using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.StateBackends;
using IStateEnrollmentCheckService = SEBT.Portal.StatesPlugins.Interfaces.IEnrollmentCheckService;
using PluginChildCheckRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.ChildCheckRequest;
using PluginChildCheckResult = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.ChildCheckResult;
using PluginEnrollmentCheckRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentCheckRequest;
using PluginEnrollmentStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentStatus;

namespace SEBT.Portal.Infrastructure.StateBackendAdapters;

/// <summary>
/// Adapts the Core enrollment-check port onto the state-connector plugin contract.
/// Builds the contract request from the Core batch, applies the flag-gated exact-match
/// guard (<see cref="EnrollmentCheckResultFilter"/>) to the connector's candidates, and
/// maps the rich contract results down to the lean Core verdicts: Match becomes IsMatch,
/// everything else does not, with confidence and status text riding along. A guarded-out
/// candidate simply vanishes from the results — its confidence and status text with it.
/// </summary>
public class PluginEnrollmentCheckBackend(
    IStateEnrollmentCheckService enrollmentCheckService,
    IConfiguration configuration,
    ILogger<PluginEnrollmentCheckBackend> logger)
    : IEnrollmentCheckBackend
{
    public async Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var pluginChildren = request.Children
            .Select(c => new PluginChildCheckRequest
            {
                // The contract correlates by Guid; Core CheckIds arriving here are the
                // handler-minted Guids, so parse rather than mint a second id.
                CheckId = Guid.Parse(c.CheckId),
                FirstName = c.FirstName,
                LastName = c.LastName,
                DateOfBirth = c.DateOfBirth,
                // The school identifier fans out to BOTH contract fields: DC's match reads
                // SchoolName and ignores SchoolCode, CO's reads SchoolCode and ignores SchoolName.
                SchoolName = c.SchoolIdentifier,
                SchoolCode = c.SchoolIdentifier,
                // AdditionalFields stays unpopulated — no connector reads it.
            })
            .ToList();

        var result = await enrollmentCheckService
            .CheckEnrollmentAsync(
                new PluginEnrollmentCheckRequest { Children = pluginChildren },
                cancellationToken)
            .ConfigureAwait(false);

        var candidates = result.Results;

        // A scoped IFeatureManager can't inject into this singleton; a per-call configuration
        // read matches house precedent and still honors AppConfig hot reload.
        if (configuration.GetValue<bool>(
                $"FeatureManagement:{FeatureFlags.EnrollmentCheckRequiresAtLeastOneExactMatchedField}"))
        {
            candidates = ApplyExactMatchGuard(pluginChildren, candidates);
        }

        return new EnrollmentCheckResult(
            candidates
                .Select(r => new EnrollmentChildResult(
                    r.CheckId.ToString(),
                    IsMatch: r.Status == PluginEnrollmentStatus.Match,
                    r.MatchConfidence,
                    r.StatusMessage))
                .ToList(),
            result.ResponseMessage);
    }

    private IList<PluginChildCheckResult> ApplyExactMatchGuard(
        IList<PluginChildCheckRequest> requestChildren,
        IList<PluginChildCheckResult> candidates)
    {
        var filtered = EnrollmentCheckResultFilter.Filter(requestChildren, candidates);
        var droppedCount = candidates.Count - filtered.Count;

        if (droppedCount > 0)
        {
            logger.LogWarning(
                "Enrollment check filter dropped {DroppedCount} of {TotalCount} candidates — neither DOB nor full name matched the submission",
                droppedCount, candidates.Count);
        }

        return filtered;
    }
}
