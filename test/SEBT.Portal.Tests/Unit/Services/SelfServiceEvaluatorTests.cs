using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class SelfServiceEvaluatorTests
{
    private static SelfServiceEvaluator CreateEvaluator(SelfServiceRulesSettings settings)
    {
        var monitor = Substitute.For<IOptionsMonitor<SelfServiceRulesSettings>>();
        monitor.CurrentValue.Returns(settings);
        return new SelfServiceEvaluator(monitor);
    }

    private static SummerEbtCase MakeCase(
        IssuanceType issuanceType,
        CardStatus cardStatus = CardStatus.Active,
        ApplicationStatus applicationStatus = ApplicationStatus.Approved,
        bool isCoLoaded = false)
        => new()
        {
            IssuanceType = issuanceType,
            EbtCardStatus = cardStatus.ToString(),
            ApplicationStatus = applicationStatus,
            IsCoLoaded = isCoLoaded
        };

    // --- DC config: SummerEbt allowed, SNAP/TANF/Unknown denied ---

    private static SelfServiceRulesSettings DcSettings() => new()
    {
        AddressUpdate = new ActionRuleSettings
        {
            Enabled = true,
            DisabledMessageKey = "selfServiceUnavailable",
            ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>
            {
                [IssuanceType.SummerEbt] = new() { Enabled = true, AllowedCardStatuses = [CardStatus.Active, CardStatus.Mailed] },
                [IssuanceType.TanfEbtCard] = new() { Enabled = false },
                [IssuanceType.SnapEbtCard] = new() { Enabled = false },
                [IssuanceType.Unknown] = new() { Enabled = false }
            }
        },
        CardReplacement = new ActionRuleSettings
        {
            Enabled = true,
            DisabledMessageKey = "selfServiceUnavailable",
            ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>
            {
                [IssuanceType.SummerEbt] = new() { Enabled = true, AllowedCardStatuses = [CardStatus.Lost, CardStatus.Stolen, CardStatus.Damaged] },
                [IssuanceType.TanfEbtCard] = new() { Enabled = false },
                [IssuanceType.SnapEbtCard] = new() { Enabled = false },
                [IssuanceType.Unknown] = new() { Enabled = false }
            }
        }
    };

    // --- CO config: both disabled at state level ---

    private static SelfServiceRulesSettings CoSettings() => new()
    {
        AddressUpdate = new ActionRuleSettings { Enabled = false },
        CardReplacement = new ActionRuleSettings { Enabled = false }
    };

    // --- Case-status-only config (for AllowedCaseStatuses dimension tests) ---

    private static SelfServiceRulesSettings CaseStatusOnlySettings(List<ApplicationStatus> allowedCaseStatuses) => new()
    {
        AddressUpdate = new ActionRuleSettings
        {
            Enabled = true,
            ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>
            {
                [IssuanceType.SummerEbt] = new()
                {
                    Enabled = true,
                    AllowedCaseStatuses = allowedCaseStatuses
                }
            }
        },
        CardReplacement = new ActionRuleSettings { Enabled = false }
    };

    // --- Both-dimensions config (AllowedCardStatuses + AllowedCaseStatuses) ---

    private static SelfServiceRulesSettings BothDimensionsSettings() => new()
    {
        AddressUpdate = new ActionRuleSettings
        {
            Enabled = true,
            ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>
            {
                [IssuanceType.SummerEbt] = new()
                {
                    Enabled = true,
                    AllowedCardStatuses = [CardStatus.Active],
                    AllowedCaseStatuses = [ApplicationStatus.Approved]
                }
            }
        },
        CardReplacement = new ActionRuleSettings { Enabled = false }
    };

    // --- Per-case evaluation ---

    [Fact]
    public void PerCase_Dc_SummerEbt_ActiveCard_CanUpdateAddress()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, CardStatus.Active);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.True(result.CanUpdateAddress);
        Assert.Null(result.AddressUpdateDeniedMessageKey);
    }

    [Fact]
    public void PerCase_Dc_SummerEbt_LostCard_CanRequestReplacement()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, CardStatus.Lost);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.True(result.CanRequestReplacementCard);
        Assert.Null(result.CardReplacementDeniedMessageKey);
    }

    [Fact]
    public void PerCase_Dc_SummerEbt_ActiveCard_CannotRequestReplacement()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, CardStatus.Active);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.False(result.CanRequestReplacementCard);
        Assert.Equal("selfServiceUnavailable", result.CardReplacementDeniedMessageKey);
    }

    [Fact]
    public void PerCase_Dc_SnapCase_DeniesAllActions()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var summerEbtCase = MakeCase(IssuanceType.SnapEbtCard, CardStatus.Active);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.False(result.CanUpdateAddress);
        Assert.False(result.CanRequestReplacementCard);
    }

    [Fact]
    public void PerCase_Dc_UnknownIssuanceType_Denied()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var summerEbtCase = MakeCase(IssuanceType.Unknown, CardStatus.Active);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.False(result.CanUpdateAddress);
        Assert.False(result.CanRequestReplacementCard);
    }

    [Fact]
    public void PerCase_CardStatusParsedFromEbtCardStatusString()
    {
        var evaluator = CreateEvaluator(DcSettings());
        // CardStatus is computed from EbtCardStatus string.
        var summerEbtCase = new SummerEbtCase
        {
            IssuanceType = IssuanceType.SummerEbt,
            EbtCardStatus = "Lost"
        };

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.True(result.CanRequestReplacementCard);
    }

    [Fact]
    public void PerCase_EmptyEbtCardStatus_FallsBackToUnknownAndDenies()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var summerEbtCase = new SummerEbtCase
        {
            IssuanceType = IssuanceType.SummerEbt,
            EbtCardStatus = null
        };

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.False(result.CanUpdateAddress);
        Assert.False(result.CanRequestReplacementCard);
    }

    // --- State-level disable ---

    [Fact]
    public void Co_AllActionsDisabled_AtStateLevel()
    {
        var evaluator = CreateEvaluator(CoSettings());
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, CardStatus.Active);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.False(result.CanUpdateAddress);
        Assert.False(result.CanRequestReplacementCard);
    }

    // --- AllowedCardStatuses dimension ---

    [Fact]
    public void EmptyAllowedCardStatuses_MeansAnyCardStatusAllowed()
    {
        var settings = new SelfServiceRulesSettings
        {
            AddressUpdate = new ActionRuleSettings
            {
                Enabled = true,
                ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>
                {
                    [IssuanceType.SummerEbt] = new() { Enabled = true, AllowedCardStatuses = [] }
                }
            },
            CardReplacement = new ActionRuleSettings { Enabled = false }
        };
        var evaluator = CreateEvaluator(settings);
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, CardStatus.Frozen);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.True(result.CanUpdateAddress);
    }

    // --- AllowedCaseStatuses dimension ---

    [Fact]
    public void AllowedCaseStatuses_Approved_AllowsApprovedCase()
    {
        var evaluator = CreateEvaluator(CaseStatusOnlySettings([ApplicationStatus.Approved]));
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Approved);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.True(result.CanUpdateAddress);
    }

    [Fact]
    public void AllowedCaseStatuses_Approved_DeniesPendingCase()
    {
        var evaluator = CreateEvaluator(CaseStatusOnlySettings([ApplicationStatus.Approved]));
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Pending);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.False(result.CanUpdateAddress);
    }

    [Fact]
    public void EmptyAllowedCaseStatuses_MeansAnyCaseStatusAllowed()
    {
        var evaluator = CreateEvaluator(CaseStatusOnlySettings([]));
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Pending);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.True(result.CanUpdateAddress);
    }

    // --- Both dimensions ANDed ---

    [Fact]
    public void BothDimensions_ApprovedAndActive_Allowed()
    {
        var evaluator = CreateEvaluator(BothDimensionsSettings());
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Approved);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.True(result.CanUpdateAddress);
    }

    [Fact]
    public void BothDimensions_ApprovedAndLost_DeniedByCardStatus()
    {
        var evaluator = CreateEvaluator(BothDimensionsSettings());
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, CardStatus.Lost, ApplicationStatus.Approved);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.False(result.CanUpdateAddress);
    }

    [Fact]
    public void BothDimensions_PendingAndActive_DeniedByCaseStatus()
    {
        var evaluator = CreateEvaluator(BothDimensionsSettings());
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Pending);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.False(result.CanUpdateAddress);
    }

    // --- Household rollup (permissive aggregation over per-case results) ---

    [Fact]
    public void HouseholdRollup_MixedCases_OneEligible_AllowsAction()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var cases = new[]
        {
            MakeCase(IssuanceType.SnapEbtCard, CardStatus.Active),
            MakeCase(IssuanceType.SummerEbt, CardStatus.Active)
        };

        var result = evaluator.EvaluateHousehold(cases);

        Assert.True(result.CanUpdateAddress);
    }

    [Fact]
    public void HouseholdRollup_NoCases_Denied()
    {
        var evaluator = CreateEvaluator(DcSettings());

        var result = evaluator.EvaluateHousehold(Array.Empty<SummerEbtCase>());

        Assert.False(result.CanUpdateAddress);
        Assert.False(result.CanRequestReplacementCard);
    }

    [Fact]
    public void HouseholdRollup_AllSnap_DeniesAllActions()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var cases = new[]
        {
            MakeCase(IssuanceType.SnapEbtCard, CardStatus.Active),
            MakeCase(IssuanceType.SnapEbtCard, CardStatus.Lost)
        };

        var result = evaluator.EvaluateHousehold(cases);

        Assert.False(result.CanUpdateAddress);
        Assert.False(result.CanRequestReplacementCard);
        Assert.Equal("selfServiceUnavailable", result.AddressUpdateDeniedMessageKey);
    }

    [Fact]
    public void HouseholdRollup_DistinctActions_AggregateIndependently()
    {
        // One case can update address (Active), another can request replacement (Lost).
        // Rollup should allow BOTH actions at the household level.
        var evaluator = CreateEvaluator(DcSettings());
        var cases = new[]
        {
            MakeCase(IssuanceType.SummerEbt, CardStatus.Active),
            MakeCase(IssuanceType.SummerEbt, CardStatus.Lost)
        };

        var result = evaluator.EvaluateHousehold(cases);

        Assert.True(result.CanUpdateAddress);
        Assert.True(result.CanRequestReplacementCard);
    }

    // --- Live reload: CurrentValue re-read per call so config file edits don't need an API restart ---

    [Fact]
    public void Evaluate_ReadsCurrentValueEachCall_ReflectsConfigReload()
    {
        var monitor = Substitute.For<IOptionsMonitor<SelfServiceRulesSettings>>();
        var denySettings = new SelfServiceRulesSettings
        {
            AddressUpdate = new ActionRuleSettings { Enabled = false },
            CardReplacement = new ActionRuleSettings { Enabled = false }
        };
        monitor.CurrentValue.Returns(denySettings);
        var evaluator = new SelfServiceEvaluator(monitor);
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Approved);

        Assert.False(evaluator.Evaluate(summerEbtCase).CanUpdateAddress);

        monitor.CurrentValue.Returns(CaseStatusOnlySettings([ApplicationStatus.Approved]));

        Assert.True(evaluator.Evaluate(summerEbtCase).CanUpdateAddress);
    }
}
