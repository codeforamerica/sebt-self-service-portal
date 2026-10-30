using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration.Validators;

/// <summary>
/// Validates SelfServiceRulesSettings at startup.
/// Ensures that enabled actions have at least one issuance type rule defined and that
/// the card replacement cooldown is set and in range.
/// </summary>
public class SelfServiceRulesSettingsValidator : IValidateOptions<SelfServiceRulesSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SelfServiceRulesSettings options)
    {
        var failures = new List<string>();

        ValidateActionRule(options.AddressUpdate, "AddressUpdate", failures);
        ValidateActionRule(options.CardReplacement, "CardReplacement", failures);

        // Cooldown applies to card replacement only, so these checks stay out of the shared helper.
        if (options.CardReplacement.CooldownDays is null)
        {
            failures.Add("CardReplacement:CooldownDays is required.");
        }
        else if (options.CardReplacement.CooldownDays is < ActionRuleSettings.MinCooldownDays or > ActionRuleSettings.MaxCooldownDays)
        {
            failures.Add(
                $"CardReplacement:CooldownDays must be between {ActionRuleSettings.MinCooldownDays} and {ActionRuleSettings.MaxCooldownDays}.");
        }

        if (options.AddressUpdate.CooldownDays is not null)
        {
            failures.Add("AddressUpdate:CooldownDays is not supported; cooldowns apply to CardReplacement only.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateActionRule(ActionRuleSettings rule, string actionName, List<string> failures)
    {
        if (rule.DisableDaysBeforeExpiration is < 0)
        {
            failures.Add($"{actionName}:DisableDaysBeforeExpiration must be greater than or equal to 0 when set.");
        }

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
