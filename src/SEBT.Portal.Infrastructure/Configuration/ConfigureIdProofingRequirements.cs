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
            }

            if (child.Value is not null)
            {
                // Simple form: "address+view": "IAL1plus"
                var level = Enum.Parse<IalLevel>(child.Value, ignoreCase: true);
                options.Requirements[child.Key] = IalRequirement.Uniform(level);
            }
            else
            {
                // Object form: "household+view": { "ApplicationCases": "IAL1plus", ... }
                var perCase = new Dictionary<string, IalLevel>();
                foreach (var sub in child.GetChildren())
                {
                    perCase[sub.Key] = Enum.Parse<IalLevel>(sub.Value!, ignoreCase: true);
                }

                options.Requirements[child.Key] = IalRequirement.PerCaseType(perCase);
            }
        }
    }
}
