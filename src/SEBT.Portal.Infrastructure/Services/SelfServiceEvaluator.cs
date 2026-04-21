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
    public AllowedActions Evaluate(SummerEbtCase summerEbtCase)
    {
        var settings = optionsMonitor.CurrentValue;
        var canUpdateAddress = IsActionAllowedForCase(settings.AddressUpdate, summerEbtCase);
        var canReplace = IsActionAllowedForCase(settings.CardReplacement, summerEbtCase);

        return BuildResult(settings, canUpdateAddress, canReplace);
    }

    public AllowedActions EvaluateHousehold(IReadOnlyList<SummerEbtCase> summerEbtCases)
    {
        var settings = optionsMonitor.CurrentValue;

        // Permissive aggregation: if any case permits the action, the household permits it.
        // Actions aggregate independently (e.g. one case may unlock address update while
        // another unlocks replacement).
        var canUpdateAddress = false;
        var canReplace = false;
        foreach (var summerEbtCase in summerEbtCases)
        {
            canUpdateAddress |= IsActionAllowedForCase(settings.AddressUpdate, summerEbtCase);
            canReplace |= IsActionAllowedForCase(settings.CardReplacement, summerEbtCase);
            if (canUpdateAddress && canReplace)
            {
                break;
            }
        }

        return BuildResult(settings, canUpdateAddress, canReplace);
    }

    private static bool IsActionAllowedForCase(ActionRuleSettings rule, SummerEbtCase summerEbtCase)
    {
        if (!rule.Enabled)
        {
            return false;
        }

        if (!rule.ByIssuanceType.TryGetValue(summerEbtCase.IssuanceType, out var typeRule))
        {
            return false;
        }

        if (!typeRule.Enabled)
        {
            return false;
        }

        // Card-status dimension: empty list means any card status is permitted.
        if (typeRule.AllowedCardStatuses.Count > 0
            && !typeRule.AllowedCardStatuses.Contains(summerEbtCase.CardStatus))
        {
            return false;
        }

        // Case-status dimension: empty list means any case status is permitted.
        if (typeRule.AllowedCaseStatuses.Count > 0
            && !typeRule.AllowedCaseStatuses.Contains(summerEbtCase.ApplicationStatus))
        {
            return false;
        }

        return true;
    }

    private static AllowedActions BuildResult(SelfServiceRulesSettings settings, bool canUpdateAddress, bool canReplace)
        => new()
        {
            CanUpdateAddress = canUpdateAddress,
            CanRequestReplacementCard = canReplace,
            AddressUpdateDeniedMessageKey = canUpdateAddress ? null : settings.AddressUpdate.DisabledMessageKey,
            CardReplacementDeniedMessageKey = canReplace ? null : settings.CardReplacement.DisabledMessageKey
        };
}
