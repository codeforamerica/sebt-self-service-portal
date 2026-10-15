using Microsoft.FeatureManagement;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginEnrollmentCheckRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentCheckRequest;
using PluginEnrollmentCheckResult = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentCheckResult;
using PluginChildCheckResult = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.ChildCheckResult;
using PluginEnrollmentStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentStatus;
using CoreEnrollmentCheckRequest = SEBT.Portal.Core.StateBackends.EnrollmentCheckRequest;
using CoreEnrollmentChild = SEBT.Portal.Core.StateBackends.EnrollmentChild;

namespace SEBT.Portal.Api.Composition;

internal sealed class FeatureGatedEnrollmentCheckService(
    IFeatureManager featureManager,
    IEnrollmentCheckService plugin,
    IEnrollmentCheckBackend? adapter) : IEnrollmentCheckService
{
    public async Task<PluginEnrollmentCheckResult> CheckEnrollmentAsync(
        PluginEnrollmentCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await ConfigurableStateBackendGate.UseAdapterAsync(featureManager, adapter))
        {
            return await plugin.CheckEnrollmentAsync(request, cancellationToken);
        }

        var children = request.Children
            .Select(c => new CoreEnrollmentChild(
                c.CheckId.ToString(),
                c.FirstName,
                c.LastName,
                c.DateOfBirth,
                c.SchoolCode ?? c.SchoolName))
            .ToList();

        EnrollmentCheckResult result = await adapter!.CheckEnrollmentAsync(
            new CoreEnrollmentCheckRequest(children),
            cancellationToken);

        var byId = request.Children.ToDictionary(c => c.CheckId.ToString(), StringComparer.Ordinal);
        var mapped = result.Results.Select(r =>
        {
            byId.TryGetValue(r.CheckId, out var submitted);
            Guid checkId = Guid.TryParse(r.CheckId, out Guid parsed) ? parsed : submitted?.CheckId ?? Guid.Empty;
            return new PluginChildCheckResult
            {
                CheckId = checkId,
                FirstName = submitted?.FirstName ?? string.Empty,
                LastName = submitted?.LastName ?? string.Empty,
                DateOfBirth = submitted?.DateOfBirth ?? default,
                Status = r.IsMatch ? PluginEnrollmentStatus.Match : PluginEnrollmentStatus.NonMatch,
                MatchConfidence = r.MatchConfidence,
                StatusMessage = r.StatusMessage,
                SchoolName = submitted?.SchoolName,
            };
        }).ToList();

        return new PluginEnrollmentCheckResult
        {
            Results = mapped,
            ResponseMessage = result.Message,
        };
    }
}
