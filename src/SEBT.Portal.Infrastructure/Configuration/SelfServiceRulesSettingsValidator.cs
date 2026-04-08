using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

public class SelfServiceRulesSettingsValidator : IValidateOptions<SelfServiceRulesSettings>
{
    public ValidateOptionsResult Validate(string? name, SelfServiceRulesSettings options)
    {
        var failures = new List<string>();

        ValidateAction("AddressUpdate", options.AddressUpdate, failures);
        ValidateAction("CardReplacement", options.CardReplacement, failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateAction(string actionName, ActionRuleSettings rule, List<string> failures)
    {
        if (rule.Enabled && rule.ByIssuanceType.Count == 0)
        {
            failures.Add(
                $"{actionName} is enabled but has no ByIssuanceType rules. " +
                "Add at least one issuance type rule or set Enabled to false.");
        }
    }
}
