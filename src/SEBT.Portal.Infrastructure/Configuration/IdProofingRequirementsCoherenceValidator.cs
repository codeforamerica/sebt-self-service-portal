using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Validates IdProofingRequirements coherence at startup and on every config reload.
/// Enforces: write >= view for the same resource, and step-up consistency.
/// </summary>
public class IdProofingRequirementsCoherenceValidator(IConfiguration configuration)
    : IValidateOptions<IdProofingRequirementsSettings>
{
    public ValidateOptionsResult Validate(string? name, IdProofingRequirementsSettings options)
    {
        var failures = new List<string>();

        CheckWriteNotBelowView(options, failures);
        CheckStepUpConsistency(options, failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(string.Join("; ", failures))
            : ValidateOptionsResult.Success;
    }

    /// <summary>
    /// For each resource that has both +view and +write requirements,
    /// every write level must be >= every view level.
    /// </summary>
    private static void CheckWriteNotBelowView(
        IdProofingRequirementsSettings options,
        List<string> failures)
    {
        var byResource = options.Requirements.Keys
            .Where(k => k.Contains('+'))
            .GroupBy(k => k.Split('+')[0], StringComparer.OrdinalIgnoreCase);

        foreach (var group in byResource)
        {
            var resource = group.Key;
            var viewKey = $"{resource}+view";
            var writeKey = $"{resource}+write";

            var hasView = options.Requirements.TryGetValue(viewKey, out var viewReq);
            var hasWrite = options.Requirements.TryGetValue(writeKey, out var writeReq);

            if (!hasView || !hasWrite)
                continue;

            foreach (var writeLevel in writeReq!.AllLevels())
                foreach (var viewLevel in viewReq!.AllLevels())
                {
                    if (writeLevel < viewLevel)
                    {
                        failures.Add(
                            $"{writeKey} level {writeLevel} is below {viewKey} level {viewLevel}. " +
                            "Write operations must require at least the same IAL as view operations.");
                    }
                }
        }
    }

    /// <summary>
    /// If OIDC step-up is configured, at least one +write requirement must be above IAL1.
    /// </summary>
    private void CheckStepUpConsistency(
        IdProofingRequirementsSettings options,
        List<string> failures)
    {
        var stepUpDiscovery = configuration["Oidc:StepUp:DiscoveryEndpoint"];
        var stepUpClientId = configuration["Oidc:StepUp:ClientId"];

        var stepUpConfigured = !string.IsNullOrWhiteSpace(stepUpDiscovery)
                               || !string.IsNullOrWhiteSpace(stepUpClientId);

        if (!stepUpConfigured)
            return;

        var anyWriteAboveIal1 = options.Requirements
            .Where(kvp => kvp.Key.EndsWith("+write", StringComparison.OrdinalIgnoreCase))
            .SelectMany(kvp => kvp.Value.AllLevels())
            .Any(level => level > IalLevel.IAL1);

        if (!anyWriteAboveIal1)
        {
            failures.Add(
                "OIDC step-up is configured (Oidc:StepUp) but no write operation requires " +
                "above IAL1. Step-up authentication will never be triggered. " +
                "Set at least one +write requirement to IAL1plus or higher.");
        }
    }
}
