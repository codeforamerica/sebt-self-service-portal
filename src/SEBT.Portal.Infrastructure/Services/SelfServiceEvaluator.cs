using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Evaluates self-service action permissions from <see cref="SelfServiceRulesSettings"/>.
/// Uses permissive aggregation: if any application in the household is eligible, the action is allowed.
/// </summary>
public class SelfServiceEvaluator(IOptionsMonitor<SelfServiceRulesSettings> optionsMonitor) : ISelfServiceEvaluator
{
    public AllowedActions Evaluate(BenefitIssuanceType householdIssuanceType, IReadOnlyList<Application> applications)
    {
        var settings = optionsMonitor.CurrentValue;
        var canUpdateAddress = IsActionAllowed(settings.AddressUpdate, householdIssuanceType, applications);
        var canReplace = IsActionAllowed(settings.CardReplacement, householdIssuanceType, applications);

        return new AllowedActions
        {
            CanUpdateAddress = canUpdateAddress,
            CanRequestReplacementCard = canReplace,
            AddressUpdateDeniedMessageKey = canUpdateAddress ? null : settings.AddressUpdate.DisabledMessageKey,
            CardReplacementDeniedMessageKey = canReplace ? null : settings.CardReplacement.DisabledMessageKey
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
            return IsIssuanceTypeAllowed(rule, fallbackType, cardStatus: null, caseStatus: null);
        }

        // Permissive aggregation: any eligible application grants access.
        foreach (var app in applications)
        {
            if (IsIssuanceTypeAllowed(rule, app.IssuanceType, app.CardStatus, app.ApplicationStatus))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIssuanceTypeAllowed(
        ActionRuleSettings rule,
        IssuanceType issuanceType,
        CardStatus? cardStatus,
        ApplicationStatus? caseStatus)
    {
        if (!rule.ByIssuanceType.TryGetValue(issuanceType, out var typeRule))
        {
            return false;
        }

        if (!typeRule.Enabled)
        {
            return false;
        }

        // Card-status dimension: empty list means any card status is permitted.
        if (typeRule.AllowedCardStatuses.Count > 0)
        {
            // No card status available (fallback path): can't check against the list.
            if (cardStatus is null)
            {
                return false;
            }

            if (!typeRule.AllowedCardStatuses.Contains(cardStatus.Value))
            {
                return false;
            }
        }

        // Case-status dimension: empty list means any case status is permitted.
        if (typeRule.AllowedCaseStatuses.Count > 0)
        {
            // No case status available (fallback path): can't check against the list.
            if (caseStatus is null)
            {
                return false;
            }

            if (!typeRule.AllowedCaseStatuses.Contains(caseStatus.Value))
            {
                return false;
            }
        }

        return true;
    }
}
