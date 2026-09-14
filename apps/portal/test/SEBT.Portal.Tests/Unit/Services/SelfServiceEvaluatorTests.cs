using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class SelfServiceEvaluatorTests
{
    private static readonly DateTimeOffset AugustFirst = new(2026, 8, 1, 12, 0, 0, TimeSpan.Zero);

    private static SelfServiceEvaluator CreateEvaluator(
        SelfServiceRulesSettings settings,
        TimeProvider? timeProvider = null,
        string timeZoneId = "America/Denver")
    {
        var monitor = Substitute.For<IOptionsMonitor<SelfServiceRulesSettings>>();
        monitor.CurrentValue.Returns(settings);
        var outageMonitor = Substitute.For<IOptionsMonitor<OutageScheduleSettings>>();
        outageMonitor.CurrentValue.Returns(new OutageScheduleSettings { TimeZoneId = timeZoneId });
        return new SelfServiceEvaluator(
            monitor,
            timeProvider ?? TimeProvider.System,
            outageMonitor,
            NullLogger<SelfServiceEvaluator>.Instance);
    }

    private static SummerEbtCase MakeCase(
        IssuanceType issuanceType,
        CardStatus cardStatus = CardStatus.Active,
        ApplicationStatus applicationStatus = ApplicationStatus.Approved,
        bool isCoLoaded = false,
        DateTime? benefitExpirationDate = null)
        => new()
        {
            IssuanceType = issuanceType,
            EbtCardStatus = cardStatus,
            ApplicationStatus = applicationStatus,
            IsCoLoaded = isCoLoaded,
            BenefitExpirationDate = benefitExpirationDate
        };

    private static SelfServiceRulesSettings ReplacementAnyStatusSettings(
        int? disableDaysBeforeExpiration = null,
        bool enabled = true)
        => new()
        {
            AddressUpdate = new ActionRuleSettings
            {
                Enabled = true,
                ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>
                {
                    [IssuanceType.SummerEbt] = new() { Enabled = true }
                }
            },
            CardReplacement = new ActionRuleSettings
            {
                Enabled = enabled,
                DisabledMessageKey = "selfServiceUnavailable",
                DisableDaysBeforeExpiration = disableDaysBeforeExpiration,
                ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>
                {
                    [IssuanceType.SummerEbt] = new() { Enabled = true, AllowedCardStatuses = [] }
                }
            }
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
                [IssuanceType.SummerEbt] = new() { Enabled = true, AllowedCardStatuses = [CardStatus.Active, CardStatus.Processed] },
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
    public void PerCase_LostCard_CanRequestReplacement_TypedEnum()
    {
        var evaluator = CreateEvaluator(DcSettings());
        var summerEbtCase = new SummerEbtCase
        {
            IssuanceType = IssuanceType.SummerEbt,
            EbtCardStatus = CardStatus.Lost
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
        var outageMonitor = Substitute.For<IOptionsMonitor<OutageScheduleSettings>>();
        outageMonitor.CurrentValue.Returns(new OutageScheduleSettings { TimeZoneId = "America/Denver" });
        var evaluator = new SelfServiceEvaluator(
            monitor,
            TimeProvider.System,
            outageMonitor,
            NullLogger<SelfServiceEvaluator>.Instance);
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, CardStatus.Active, ApplicationStatus.Approved);

        Assert.False(evaluator.Evaluate(summerEbtCase).CanUpdateAddress);

        monitor.CurrentValue.Returns(CaseStatusOnlySettings([ApplicationStatus.Approved]));

        Assert.True(evaluator.Evaluate(summerEbtCase).CanUpdateAddress);
    }

    // --- DisableDaysBeforeExpiration (per-case cutoff, independent of Enabled) ---

    [Fact]
    public void PerCase_WithinCutoff_DeniesReplacement()
    {
        var evaluator = CreateEvaluator(
            ReplacementAnyStatusSettings(disableDaysBeforeExpiration: 4),
            new FakeTimeProvider(AugustFirst));
        var summerEbtCase = MakeCase(
            IssuanceType.SummerEbt,
            benefitExpirationDate: new DateTime(2026, 8, 5));

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.False(result.CanRequestReplacementCard);
        Assert.Equal("selfServiceUnavailable", result.CardReplacementDeniedMessageKey);
    }

    [Fact]
    public void PerCase_OutsideCutoff_AllowsReplacement()
    {
        var evaluator = CreateEvaluator(
            ReplacementAnyStatusSettings(disableDaysBeforeExpiration: 4),
            new FakeTimeProvider(AugustFirst));
        var summerEbtCase = MakeCase(
            IssuanceType.SummerEbt,
            benefitExpirationDate: new DateTime(2026, 8, 7));

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.True(result.CanRequestReplacementCard);
        Assert.Null(result.CardReplacementDeniedMessageKey);
    }

    [Fact]
    public void HouseholdRollup_MixedExpirationDates_AllowsWhenAnyCaseIsOutsideCutoff()
    {
        var evaluator = CreateEvaluator(
            ReplacementAnyStatusSettings(disableDaysBeforeExpiration: 4),
            new FakeTimeProvider(AugustFirst));
        var cases = new[]
        {
            MakeCase(IssuanceType.SummerEbt, benefitExpirationDate: new DateTime(2026, 8, 5)),
            MakeCase(IssuanceType.SummerEbt, benefitExpirationDate: new DateTime(2026, 8, 5)),
            MakeCase(IssuanceType.SummerEbt, benefitExpirationDate: new DateTime(2026, 8, 7))
        };

        var household = evaluator.EvaluateHousehold(cases);

        Assert.True(household.CanRequestReplacementCard);
        Assert.False(evaluator.Evaluate(cases[0]).CanRequestReplacementCard);
        Assert.False(evaluator.Evaluate(cases[1]).CanRequestReplacementCard);
        Assert.True(evaluator.Evaluate(cases[2]).CanRequestReplacementCard);
    }

    [Fact]
    public void PerCase_CutoffOmitted_AllowsRegardlessOfExpiration()
    {
        var evaluator = CreateEvaluator(
            ReplacementAnyStatusSettings(disableDaysBeforeExpiration: null),
            new FakeTimeProvider(AugustFirst));
        var summerEbtCase = MakeCase(
            IssuanceType.SummerEbt,
            benefitExpirationDate: new DateTime(2026, 8, 5));

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.True(result.CanRequestReplacementCard);
    }

    [Fact]
    public void PerCase_StatewideDisabled_DeniesEvenOutsideCutoff()
    {
        var evaluator = CreateEvaluator(
            ReplacementAnyStatusSettings(disableDaysBeforeExpiration: 4, enabled: false),
            new FakeTimeProvider(AugustFirst));
        var summerEbtCase = MakeCase(
            IssuanceType.SummerEbt,
            benefitExpirationDate: new DateTime(2026, 8, 7));

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.False(result.CanRequestReplacementCard);
    }

    [Fact]
    public void PerCase_NullExpiration_DoesNotApplyCutoff()
    {
        var evaluator = CreateEvaluator(
            ReplacementAnyStatusSettings(disableDaysBeforeExpiration: 4),
            new FakeTimeProvider(AugustFirst));
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, benefitExpirationDate: null);

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.True(result.CanRequestReplacementCard);
    }

    [Fact]
    public void PerCase_AlreadyExpired_DeniesWhenCutoffSet()
    {
        var evaluator = CreateEvaluator(
            ReplacementAnyStatusSettings(disableDaysBeforeExpiration: 4),
            new FakeTimeProvider(AugustFirst));
        var summerEbtCase = MakeCase(
            IssuanceType.SummerEbt,
            benefitExpirationDate: new DateTime(2026, 7, 15));

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.False(result.CanRequestReplacementCard);
    }

    [Fact]
    public void PerCase_ExpirationDay_WithZeroDays_Denies()
    {
        var evaluator = CreateEvaluator(
            ReplacementAnyStatusSettings(disableDaysBeforeExpiration: 0),
            new FakeTimeProvider(AugustFirst));
        var summerEbtCase = MakeCase(
            IssuanceType.SummerEbt,
            benefitExpirationDate: new DateTime(2026, 8, 1));

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.False(result.CanRequestReplacementCard);
    }

    [Fact]
    public void PerCase_CutoffDoesNotAffectAddressUpdate()
    {
        var evaluator = CreateEvaluator(
            ReplacementAnyStatusSettings(disableDaysBeforeExpiration: 4),
            new FakeTimeProvider(AugustFirst));
        var summerEbtCase = MakeCase(
            IssuanceType.SummerEbt,
            benefitExpirationDate: new DateTime(2026, 8, 5));

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.True(result.CanUpdateAddress);
        Assert.False(result.CanRequestReplacementCard);
    }

    [Fact]
    public void PerCase_CutoffUsesStateLocalDate_NotUtc()
    {
        // 2026-08-03 00:30 UTC is still 2026-08-02 18:30 in America/Denver.
        // Expiration Aug 7, N=4 → cutoff Aug 3. Local Aug 2 is still allowed; UTC Aug 3 would deny.
        var evaluator = CreateEvaluator(
            ReplacementAnyStatusSettings(disableDaysBeforeExpiration: 4),
            new FakeTimeProvider(new DateTimeOffset(2026, 8, 3, 0, 30, 0, TimeSpan.Zero)),
            timeZoneId: "America/Denver");
        var summerEbtCase = MakeCase(
            IssuanceType.SummerEbt,
            benefitExpirationDate: new DateTime(2026, 8, 7));

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.True(result.CanRequestReplacementCard);
    }

    [Fact]
    public void PerCase_InvalidTimeZone_FallsBackToUtcDate()
    {
        var evaluator = CreateEvaluator(
            ReplacementAnyStatusSettings(disableDaysBeforeExpiration: 4),
            new FakeTimeProvider(new DateTimeOffset(2026, 8, 3, 0, 30, 0, TimeSpan.Zero)),
            timeZoneId: "Not/A_Real_Zone");
        var summerEbtCase = MakeCase(
            IssuanceType.SummerEbt,
            benefitExpirationDate: new DateTime(2026, 8, 7));

        var result = evaluator.Evaluate(summerEbtCase);

        Assert.False(result.CanRequestReplacementCard);
    }
}
