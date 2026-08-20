using SEBT.Portal.Core.StateConnector;
using IPluginEnrollmentCheckService = SEBT.Portal.StatesPlugins.Interfaces.IEnrollmentCheckService;
using PluginChildCheckRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.ChildCheckRequest;
using PluginChildCheckResult = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.ChildCheckResult;
using PluginEligibilityType = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EligibilityType;
using PluginEnrollmentCheckRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentCheckRequest;
using PluginEnrollmentStatus = SEBT.Portal.StatesPlugins.Interfaces.Models.EnrollmentCheck.EnrollmentStatus;

namespace SEBT.Portal.Infrastructure.StateConnector;

/// <summary>
/// Adapter that fulfills the Core <see cref="IStateEnrollmentCheckService"/> port by
/// delegating to the loaded state plugin's enrollment check service. Maps Core models
/// to plugin contract models (and back) at the boundary.
/// </summary>
public class PluginEnrollmentCheckService(
    IPluginEnrollmentCheckService pluginEnrollmentCheckService) : IStateEnrollmentCheckService
{
    /// <inheritdoc />
    public async Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request,
        CancellationToken cancellationToken = default)
    {
        var pluginRequest = new PluginEnrollmentCheckRequest
        {
            Children = request.Children.Select(MapToPluginChild).ToList(),
            GuardianContactInfo = request.GuardianContactInfo
        };

        var pluginResult = await pluginEnrollmentCheckService.CheckEnrollmentAsync(
            pluginRequest, cancellationToken);

        return new EnrollmentCheckResult
        {
            Results = pluginResult.Results.Select(MapToCoreChildResult).ToList(),
            ResponseMessage = pluginResult.ResponseMessage
        };
    }

    private static PluginChildCheckRequest MapToPluginChild(ChildCheckRequest child) =>
        new()
        {
            CheckId = child.CheckId,
            FirstName = child.FirstName,
            LastName = child.LastName,
            DateOfBirth = child.DateOfBirth,
            SchoolName = child.SchoolName,
            SchoolCode = child.SchoolCode,
            AdditionalFields = CopyOrEmpty(child.AdditionalFields)
        };

    private static ChildCheckResult MapToCoreChildResult(PluginChildCheckResult result) =>
        new()
        {
            CheckId = result.CheckId,
            FirstName = result.FirstName,
            LastName = result.LastName,
            DateOfBirth = result.DateOfBirth,
            Status = MapStatus(result.Status),
            MatchConfidence = result.MatchConfidence,
            StatusMessage = result.StatusMessage,
            EligibilityType = result.EligibilityType == null
                ? null
                : MapEligibilityType(result.EligibilityType.Value),
            SchoolName = result.SchoolName,
            Details = CopyOrEmpty(result.Details)
        };

    // The dictionary properties default to non-null, but init assignment or
    // deserialization can still leave them null; copy null as empty rather than
    // letting the Dictionary copy constructor throw.
    private static Dictionary<TKey, TValue> CopyOrEmpty<TKey, TValue>(IDictionary<TKey, TValue>? source)
        where TKey : notnull =>
        source is null ? [] : new Dictionary<TKey, TValue>(source);

    // Explicit switches (not casts) so a contract enum change fails loudly at compile
    // or run time instead of silently mapping to the wrong Core value.
    private static EnrollmentStatus MapStatus(PluginEnrollmentStatus status) =>
        status switch
        {
            PluginEnrollmentStatus.Match => EnrollmentStatus.Match,
            PluginEnrollmentStatus.PossibleMatch => EnrollmentStatus.PossibleMatch,
            PluginEnrollmentStatus.NonMatch => EnrollmentStatus.NonMatch,
            PluginEnrollmentStatus.Error => EnrollmentStatus.Error,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown enrollment status from state plugin.")
        };

    private static EligibilityType MapEligibilityType(PluginEligibilityType eligibilityType) =>
        eligibilityType switch
        {
            PluginEligibilityType.Unknown => EligibilityType.Unknown,
            PluginEligibilityType.Snap => EligibilityType.Snap,
            PluginEligibilityType.Tanf => EligibilityType.Tanf,
            PluginEligibilityType.Frp => EligibilityType.Frp,
            PluginEligibilityType.DirectCert => EligibilityType.DirectCert,
            _ => throw new ArgumentOutOfRangeException(nameof(eligibilityType), eligibilityType, "Unknown eligibility type from state plugin.")
        };
}
