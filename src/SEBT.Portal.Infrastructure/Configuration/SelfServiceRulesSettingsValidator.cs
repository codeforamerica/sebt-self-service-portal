using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Validates SelfServiceRulesSettings at startup.
/// Ensures that enabled actions have at least one issuance type rule defined.
/// </summary>
public class SelfServiceRulesSettingsValidator : IValidateOptions<SelfServiceRulesSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SelfServiceRulesSettings options)
    {
        var failures = new List<string>();

        ValidateActionRule(options.AddressUpdate, "AddressUpdate", failures);
        ValidateActionRule(options.CardReplacement, "CardReplacement", failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateActionRule(ActionRuleSettings rule, string actionName, List<string> failures)
    {
        if (!rule.Enabled)
        {
            return;
        }

        if (rule.ByIssuanceType.Count == 0)
        {
            failures.Add($"{actionName} is enabled but has no issuance type rules defined in ByIssuanceType.");
        }
    }
}
