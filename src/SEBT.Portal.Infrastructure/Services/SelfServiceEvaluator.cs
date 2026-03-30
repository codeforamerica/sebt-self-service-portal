using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Evaluates self-service action permissions from <see cref="SelfServiceRulesSettings"/>.
/// Uses permissive aggregation: if any application in the household is eligible, the action is allowed.
/// </summary>
public class SelfServiceEvaluator(IOptions<SelfServiceRulesSettings> options) : ISelfServiceEvaluator
{
    private readonly SelfServiceRulesSettings _settings = options.Value;

    public AllowedActions Evaluate(BenefitIssuanceType householdIssuanceType, IReadOnlyList<Application> applications)
    {
        var canUpdateAddress = IsActionAllowed(_settings.AddressUpdate, householdIssuanceType, applications);
        var canReplace = IsActionAllowed(_settings.CardReplacement, householdIssuanceType, applications);

        return new AllowedActions
        {
            CanUpdateAddress = canUpdateAddress,
            CanRequestReplacementCard = canReplace,
            AddressUpdateDeniedMessageKey = canUpdateAddress ? null : _settings.AddressUpdate.DisabledMessageKey,
            CardReplacementDeniedMessageKey = canReplace ? null : _settings.CardReplacement.DisabledMessageKey
        };
    }

    private static bool IsActionAllowed(
        ActionRuleSettings rule,
        BenefitIssuanceType householdIssuanceType,
        IReadOnlyList<Application> applications)
    {
        if (!rule.Enabled)
        {
            return false;
        }

        // When no applications exist, fall back to the household-level issuance type.
        if (applications.Count == 0)
        {
            var fallbackType = (IssuanceType)householdIssuanceType;
            return IsIssuanceTypeAllowed(rule, fallbackType, cardStatus: null);
        }

        // Permissive aggregation: any eligible application grants access.
        foreach (var app in applications)
        {
            if (IsIssuanceTypeAllowed(rule, app.IssuanceType, app.CardStatus))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIssuanceTypeAllowed(ActionRuleSettings rule, IssuanceType issuanceType, CardStatus? cardStatus)
    {
        if (!rule.ByIssuanceType.TryGetValue(issuanceType, out var typeRule))
        {
            return false;
        }

        if (!typeRule.Enabled)
        {
            return false;
        }

        // Empty AllowedCardStatuses means any card status is permitted.
        if (typeRule.AllowedCardStatuses.Count == 0)
        {
            return true;
        }

        // No card status available (fallback path): can't check against the list.
        if (cardStatus is null)
        {
            return false;
        }

        return typeRule.AllowedCardStatuses.Contains(cardStatus.Value);
    }
}
