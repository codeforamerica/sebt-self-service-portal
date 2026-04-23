using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Custom config binder for IdProofingRequirements. Handles the polymorphic
/// value format: each key can be a simple string ("IAL1plus") or an object
/// with per-case-type sub-requirements.
/// </summary>
public class ConfigureIdProofingRequirements(
    IConfiguration config,
    ILogger<ConfigureIdProofingRequirements> logger)
    : IConfigureOptions<IdProofingRequirementsSettings>
{
    public void Configure(IdProofingRequirementsSettings options)
    {
        var section = config.GetSection(IdProofingRequirementsSettings.SectionName);
        options.Requirements.Clear();

        foreach (var child in section.GetChildren())
        {
            if (!IdProofingKeys.AllValidKeys.Contains(child.Key))
            {
                logger.LogWarning(
                    "Unrecognized IdProofingRequirements key '{Key}'. " +
                    "Valid keys are resource+action combinations: {ValidKeys}",
                    child.Key,
                    string.Join(", ", IdProofingKeys.AllValidKeys));
                continue;
            }

            if (child.Value is not null)
            {
                // Simple form: "address+view": "IAL1plus"
                if (!Enum.TryParse<IalLevel>(child.Value, ignoreCase: true, out var level))
                {
                    logger.LogError(
                        "Invalid IalLevel value '{Value}' for IdProofingRequirements key '{Key}'. " +
                        "Valid values: IAL1, IAL1plus, IAL2. This key will default to IAL1plus (fail-safe).",
                        child.Value, child.Key);
                    continue;
                }

                options.Requirements[child.Key] = IalRequirement.Uniform(level);
            }
            else
            {
                // Object form: "household+view": { "application": "IAL1plus", ... }
                var perCase = new Dictionary<string, IalLevel>(StringComparer.OrdinalIgnoreCase);
                var hasError = false;
                foreach (var sub in child.GetChildren())
                {
                    if (sub.Value is null || !Enum.TryParse<IalLevel>(sub.Value, ignoreCase: true, out var subLevel))
                    {
                        logger.LogError(
                            "Invalid IalLevel value '{Value}' for IdProofingRequirements key '{Key}:{SubKey}'. " +
                            "Valid values: IAL1, IAL1plus, IAL2. This key will default to IAL1plus (fail-safe).",
                            sub.Value ?? "(null)", child.Key, sub.Key);
                        hasError = true;
                        continue;
                    }

                    perCase[sub.Key] = subLevel;
                }

                // Partial success is intentional: valid sub-keys are kept; missing/invalid
                // ones fall through to IalRequirement.ClassifyCase's fail-safe default
                // (IAL1plus), so a single bad sub-key doesn't discard its valid siblings.
                if (!hasError || perCase.Count > 0)
                {
                    options.Requirements[child.Key] = IalRequirement.PerCaseType(perCase);
                }
            }
        }
    }
}
