using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Evaluates self-service action permissions from SelfServiceRulesSettings.
/// Household-level uses permissive aggregation; per-case evaluates individually.
/// </summary>
public class SelfServiceEvaluator(IOptions<SelfServiceRulesSettings> options) : ISelfServiceEvaluator
{
    private readonly SelfServiceRulesSettings _settings = options.Value;

    public HouseholdAllowedActions EvaluateHousehold(IReadOnlyList<SummerEbtCase> cases)
    {
        var canUpdate = IsHouseholdActionAllowed(_settings.AddressUpdate, cases);

        return new HouseholdAllowedActions
        {
            CanUpdateAddress = canUpdate,
            AddressUpdateDeniedMessageKey = canUpdate ? null : _settings.AddressUpdate.DisabledMessageKey
        };
    }

    public CaseAllowedActions EvaluateCase(SummerEbtCase summerEbtCase)
    {
        var canReplace = IsCaseActionAllowed(_settings.CardReplacement, summerEbtCase);

        return new CaseAllowedActions
        {
            CanRequestReplacementCard = canReplace,
            CardReplacementDeniedMessageKey = canReplace ? null : _settings.CardReplacement.DisabledMessageKey
        };
    }

    private static bool IsHouseholdActionAllowed(ActionRuleSettings rule, IReadOnlyList<SummerEbtCase> cases)
    {
        if (!rule.Enabled)
            return false;

        // Permissive aggregation: any eligible case grants access.
        foreach (var c in cases)
        {
            if (IsIssuanceTypeAllowed(rule, c.IssuanceType, ParseCardStatus(c.EbtCardStatus)))
                return true;
        }

        return false;
    }

    private static bool IsCaseActionAllowed(ActionRuleSettings rule, SummerEbtCase summerEbtCase)
    {
        if (!rule.Enabled)
            return false;

        return IsIssuanceTypeAllowed(rule, summerEbtCase.IssuanceType, ParseCardStatus(summerEbtCase.EbtCardStatus));
    }

    private static bool IsIssuanceTypeAllowed(ActionRuleSettings rule, IssuanceType issuanceType, CardStatus? cardStatus)
    {
        if (!rule.ByIssuanceType.TryGetValue(issuanceType, out var typeRule))
            return false;

        if (!typeRule.Enabled)
            return false;

        if (typeRule.AllowedCardStatuses.Count == 0)
            return true;

        if (cardStatus is null)
            return false;

        return typeRule.AllowedCardStatuses.Contains(cardStatus.Value);
    }

    private static CardStatus? ParseCardStatus(string? ebtCardStatus)
    {
        if (string.IsNullOrEmpty(ebtCardStatus))
            return null;

        return Enum.TryParse<CardStatus>(ebtCardStatus, ignoreCase: true, out var status) ? status : null;
    }
}
