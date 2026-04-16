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

    private static Application MakeApp(IssuanceType issuanceType, CardStatus cardStatus = CardStatus.Active)
        => new() { IssuanceType = issuanceType, CardStatus = cardStatus };

    private static Application MakeApp(
        IssuanceType issuanceType,
        CardStatus cardStatus,
        ApplicationStatus applicationStatus)
        => new()
        {
            IssuanceType = issuanceType,
            CardStatus = cardStatus,
            ApplicationStatus = applicationStatus
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

    // DC scenarios

    [Fact]
    public void Dc_SummerEbt_ActiveCard_CanUpdateAddress()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var apps = new[] { MakeApp(IssuanceType.SummerEbt, CardStatus.Active) };

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps);

        Assert.True(result.CanUpdateAddress);
        Assert.Null(result.AddressUpdateDeniedMessageKey);
    }

    [Fact]
    public void Dc_SummerEbt_LostCard_CanRequestReplacement()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var apps = new[] { MakeApp(IssuanceType.SummerEbt, CardStatus.Lost) };

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps);

        Assert.True(result.CanRequestReplacementCard);
        Assert.Null(result.CardReplacementDeniedMessageKey);
    }

    [Fact]
    public void Dc_SummerEbt_ActiveCard_CannotRequestReplacement()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var apps = new[] { MakeApp(IssuanceType.SummerEbt, CardStatus.Active) };

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps);

        Assert.False(result.CanRequestReplacementCard);
        Assert.Equal("selfServiceUnavailable", result.CardReplacementDeniedMessageKey);
    }

    [Fact]
    public void Dc_SnapUser_CannotUpdateAddress()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var apps = new[] { MakeApp(IssuanceType.SnapEbtCard, CardStatus.Active) };

        var result = evaluator.Evaluate(BenefitIssuanceType.SnapEbtCard, apps);

        Assert.False(result.CanUpdateAddress);
        Assert.Equal("selfServiceUnavailable", result.AddressUpdateDeniedMessageKey);
    }

    [Fact]
    public void Dc_SnapUser_CannotRequestReplacement()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var apps = new[] { MakeApp(IssuanceType.SnapEbtCard, CardStatus.Lost) };

        var result = evaluator.Evaluate(BenefitIssuanceType.SnapEbtCard, apps);

        Assert.False(result.CanRequestReplacementCard);
        Assert.Equal("selfServiceUnavailable", result.CardReplacementDeniedMessageKey);
    }

    // Permissive aggregation (D9)

    [Fact]
    public void Dc_MixedHousehold_OneEligible_AllowsAction()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var apps = new[]
        {
            MakeApp(IssuanceType.SnapEbtCard, CardStatus.Active),
            MakeApp(IssuanceType.SummerEbt, CardStatus.Active)
        };

        var result = evaluator.Evaluate(BenefitIssuanceType.SnapEbtCard, apps);

        Assert.True(result.CanUpdateAddress);
    }

    [Fact]
    public void Dc_MixedHousehold_NoneEligible_DeniesAction()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var apps = new[]
        {
            MakeApp(IssuanceType.SnapEbtCard, CardStatus.Active),
            MakeApp(IssuanceType.TanfEbtCard, CardStatus.Active)
        };

        var result = evaluator.Evaluate(BenefitIssuanceType.SnapEbtCard, apps);

        Assert.False(result.CanUpdateAddress);
    }

    // CO scenarios

    [Fact]
    public void Co_AllActionsDisabled_AtStateLevel()
    {
        var evaluator = CreateEvaluator(CoSettings());
        var apps = new[] { MakeApp(IssuanceType.SummerEbt, CardStatus.Active) };

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps);

        Assert.False(result.CanUpdateAddress);
        Assert.False(result.CanRequestReplacementCard);
    }

    // Edge cases

    [Fact]
    public void EmptyApplications_FallsBackToHouseholdIssuanceType()
    {
        // Config allows SummerEbt with empty AllowedCardStatuses (any status)
        var settings = new SelfServiceRulesSettings
        {
            AddressUpdate = new ActionRuleSettings
            {
                Enabled = true,
                ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>
                {
                    [IssuanceType.SummerEbt] = new() { Enabled = true }
                }
            },
            CardReplacement = new ActionRuleSettings { Enabled = false }
        };
        var evaluator = CreateEvaluator(settings);

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, Array.Empty<Application>());

        Assert.True(result.CanUpdateAddress);
    }

    [Fact]
    public void EmptyApplications_UnknownIssuanceType_Denied()
    {
        var evaluator = CreateEvaluator(DcSettings());

        var result = evaluator.Evaluate(BenefitIssuanceType.Unknown, Array.Empty<Application>());

        Assert.False(result.CanUpdateAddress);
        Assert.False(result.CanRequestReplacementCard);
    }

    [Fact]
    public void IssuanceTypeNotInConfig_Denied()
    {
        // Config only has SummerEbt, but we pass TanfEbtCard which is Enabled=false
        var evaluator = CreateEvaluator(DcSettings());
        var apps = new[] { MakeApp(IssuanceType.TanfEbtCard, CardStatus.Active) };

        var result = evaluator.Evaluate(BenefitIssuanceType.TanfEbtCard, apps);

        Assert.False(result.CanUpdateAddress);
    }

    [Fact]
    public void EmptyAllowedCardStatuses_MeansAnyStatusAllowed()
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
        var apps = new[] { MakeApp(IssuanceType.SummerEbt, CardStatus.Frozen) };

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps);

        Assert.True(result.CanUpdateAddress);
    }

    // --- AllowedCaseStatuses dimension ---

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

    [Fact]
    public void AllowedCaseStatuses_Approved_AllowsApprovedApplication()
    {
        var evaluator = CreateEvaluator(CaseStatusOnlySettings([ApplicationStatus.Approved]));
        var apps = new[] { MakeApp(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Approved) };

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps);

        Assert.True(result.CanUpdateAddress);
    }

    [Fact]
    public void AllowedCaseStatuses_Approved_DeniesPendingApplication()
    {
        var evaluator = CreateEvaluator(CaseStatusOnlySettings([ApplicationStatus.Approved]));
        var apps = new[] { MakeApp(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Pending) };

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps);

        Assert.False(result.CanUpdateAddress);
    }

    [Fact]
    public void EmptyAllowedCaseStatuses_MeansAnyCaseStatusAllowed()
    {
        var evaluator = CreateEvaluator(CaseStatusOnlySettings([]));
        var apps = new[] { MakeApp(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Pending) };

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps);

        Assert.True(result.CanUpdateAddress);
    }

    // --- Both dimensions AND ---

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

    [Fact]
    public void BothDimensions_ApprovedAndActive_Allowed()
    {
        var evaluator = CreateEvaluator(BothDimensionsSettings());
        var apps = new[] { MakeApp(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Approved) };

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps);

        Assert.True(result.CanUpdateAddress);
    }

    [Fact]
    public void BothDimensions_ApprovedAndLost_DeniedByCardStatus()
    {
        var evaluator = CreateEvaluator(BothDimensionsSettings());
        var apps = new[] { MakeApp(IssuanceType.SummerEbt, CardStatus.Lost, ApplicationStatus.Approved) };

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps);

        Assert.False(result.CanUpdateAddress);
    }

    [Fact]
    public void BothDimensions_PendingAndActive_DeniedByCaseStatus()
    {
        var evaluator = CreateEvaluator(BothDimensionsSettings());
        var apps = new[] { MakeApp(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Pending) };

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps);

        Assert.False(result.CanUpdateAddress);
    }

    // --- Permissive aggregation across apps with case-status gating ---

    [Fact]
    public void PermissiveAggregation_OneApprovedApp_AllowsAction()
    {
        var evaluator = CreateEvaluator(CaseStatusOnlySettings([ApplicationStatus.Approved]));
        var apps = new[]
        {
            MakeApp(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Pending),
            MakeApp(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Approved)
        };

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps);

        Assert.True(result.CanUpdateAddress);
    }

    // --- Fallback path (no applications) with case-status gating ---

    [Fact]
    public void EmptyApplications_NonEmptyAllowedCaseStatuses_Denied()
    {
        var evaluator = CreateEvaluator(CaseStatusOnlySettings([ApplicationStatus.Approved]));

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, Array.Empty<Application>());

        Assert.False(result.CanUpdateAddress);
    }

    [Fact]
    public void EmptyApplications_EmptyAllowedCaseStatuses_FallbackAllowed()
    {
        var evaluator = CreateEvaluator(CaseStatusOnlySettings([]));

        var result = evaluator.Evaluate(BenefitIssuanceType.SummerEbt, Array.Empty<Application>());

        Assert.True(result.CanUpdateAddress);
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
        var apps = new[] { MakeApp(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Approved) };

        Assert.False(evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps).CanUpdateAddress);

        monitor.CurrentValue.Returns(CaseStatusOnlySettings([ApplicationStatus.Approved]));

        Assert.True(evaluator.Evaluate(BenefitIssuanceType.SummerEbt, apps).CanUpdateAddress);
    }
}
