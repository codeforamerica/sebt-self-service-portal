using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.Services;

public class SelfServiceEvaluatorTests
{
    // -------------------------------------------------------------------------
    // Config helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// DC-like config: AddressUpdate enabled for SummerEbt (Active status).
    /// CardReplacement enabled for SummerEbt with Lost/Stolen/Damaged statuses.
    /// </summary>
    private static SelfServiceRulesSettings DcSettings() => new()
    {
        AddressUpdate = new ActionRuleSettings
        {
            Enabled = true,
            DisabledMessageKey = "dashboard.addressUpdateDisabled",
            ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>
            {
                [IssuanceType.SummerEbt] = new IssuanceTypeRuleSettings
                {
                    Enabled = true,
                    AllowedCardStatuses = [CardStatus.Active]
                }
            }
        },
        CardReplacement = new ActionRuleSettings
        {
            Enabled = true,
            DisabledMessageKey = "dashboard.cardReplacementDisabled",
            ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>
            {
                [IssuanceType.SummerEbt] = new IssuanceTypeRuleSettings
                {
                    Enabled = true,
                    AllowedCardStatuses = [CardStatus.Lost, CardStatus.Stolen, CardStatus.Damaged]
                }
            }
        }
    };

    /// <summary>
    /// CO-like config: both actions disabled at the top level.
    /// </summary>
    private static SelfServiceRulesSettings CoSettings() => new()
    {
        AddressUpdate = new ActionRuleSettings
        {
            Enabled = false,
            DisabledMessageKey = "dashboard.addressUpdateDisabled"
        },
        CardReplacement = new ActionRuleSettings
        {
            Enabled = false,
            DisabledMessageKey = "dashboard.cardReplacementDisabled"
        }
    };

    private static SelfServiceEvaluator Create(SelfServiceRulesSettings settings)
    {
        var snapshot = Substitute.For<IOptionsSnapshot<SelfServiceRulesSettings>>();
        snapshot.Value.Returns(settings);
        return new SelfServiceEvaluator(snapshot);
    }

    // -------------------------------------------------------------------------
    // Case factory
    // -------------------------------------------------------------------------

    private static SummerEbtCase MakeCase(
        IssuanceType issuanceType,
        string? ebtCardStatus = "Active",
        string? caseId = "SEBT-001")
        => new()
        {
            SummerEBTCaseID = caseId,
            IssuanceType = issuanceType,
            EbtCardStatus = ebtCardStatus
        };

    // =========================================================================
    // EvaluateHousehold
    // =========================================================================

    [Fact]
    public void EvaluateHousehold_DcConfig_SummerEbtWithActive_CanUpdateAddress()
    {
        var evaluator = Create(DcSettings());
        var cases = new[] { MakeCase(IssuanceType.SummerEbt, "Active") };

        var result = evaluator.EvaluateHousehold(cases);

        Assert.True(result.CanUpdateAddress);
        Assert.Null(result.AddressUpdateDeniedMessageKey);
    }

    [Fact]
    public void EvaluateHousehold_DcConfig_SnapCase_CannotUpdateAddress()
    {
        var evaluator = Create(DcSettings());
        var cases = new[] { MakeCase(IssuanceType.SnapEbtCard, "Active") };

        var result = evaluator.EvaluateHousehold(cases);

        Assert.False(result.CanUpdateAddress);
        Assert.Equal("dashboard.addressUpdateDisabled", result.AddressUpdateDeniedMessageKey);
    }

    [Fact]
    public void EvaluateHousehold_DcConfig_MixedHousehold_PermissiveAggregation_CanUpdateAddress()
    {
        var evaluator = Create(DcSettings());
        // SNAP case alone would be denied; the SummerEbt case grants access for the whole household.
        var cases = new[]
        {
            MakeCase(IssuanceType.SnapEbtCard, "Active", "SEBT-001"),
            MakeCase(IssuanceType.SummerEbt, "Active", "SEBT-002")
        };

        var result = evaluator.EvaluateHousehold(cases);

        Assert.True(result.CanUpdateAddress);
        Assert.Null(result.AddressUpdateDeniedMessageKey);
    }

    [Fact]
    public void EvaluateHousehold_CoConfig_CannotUpdateAddress()
    {
        var evaluator = Create(CoSettings());
        var cases = new[] { MakeCase(IssuanceType.SummerEbt, "Active") };

        var result = evaluator.EvaluateHousehold(cases);

        Assert.False(result.CanUpdateAddress);
        Assert.Equal("dashboard.addressUpdateDisabled", result.AddressUpdateDeniedMessageKey);
    }

    [Fact]
    public void EvaluateHousehold_EmptyCasesList_CannotUpdateAddress()
    {
        var evaluator = Create(DcSettings());

        var result = evaluator.EvaluateHousehold([]);

        Assert.False(result.CanUpdateAddress);
        Assert.Equal("dashboard.addressUpdateDisabled", result.AddressUpdateDeniedMessageKey);
    }

    // =========================================================================
    // EvaluateCase
    // =========================================================================

    [Fact]
    public void EvaluateCase_DcConfig_SummerEbtWithLost_CanRequestReplacementCard()
    {
        var evaluator = Create(DcSettings());
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, "Lost");

        var result = evaluator.EvaluateCase(summerEbtCase);

        Assert.True(result.CanRequestReplacementCard);
        Assert.Null(result.CardReplacementDeniedMessageKey);
    }

    [Fact]
    public void EvaluateCase_DcConfig_SummerEbtWithActive_CannotRequestReplacementCard()
    {
        var evaluator = Create(DcSettings());
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, "Active");

        var result = evaluator.EvaluateCase(summerEbtCase);

        Assert.False(result.CanRequestReplacementCard);
        Assert.Equal("dashboard.cardReplacementDisabled", result.CardReplacementDeniedMessageKey);
    }

    [Fact]
    public void EvaluateCase_DcConfig_SnapCaseWithLost_CannotRequestReplacementCard()
    {
        var evaluator = Create(DcSettings());
        // SNAP is not in CardReplacement.ByIssuanceType, so it should be denied.
        var summerEbtCase = MakeCase(IssuanceType.SnapEbtCard, "Lost");

        var result = evaluator.EvaluateCase(summerEbtCase);

        Assert.False(result.CanRequestReplacementCard);
        Assert.Equal("dashboard.cardReplacementDisabled", result.CardReplacementDeniedMessageKey);
    }

    [Fact]
    public void EvaluateCase_CoConfig_CannotRequestReplacementCard()
    {
        var evaluator = Create(CoSettings());
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, "Lost");

        var result = evaluator.EvaluateCase(summerEbtCase);

        Assert.False(result.CanRequestReplacementCard);
        Assert.Equal("dashboard.cardReplacementDisabled", result.CardReplacementDeniedMessageKey);
    }

    [Fact]
    public void EvaluateCase_CardStatusParsing_CaseInsensitive_MatchesLost()
    {
        var evaluator = Create(DcSettings());
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, "LOST");

        var result = evaluator.EvaluateCase(summerEbtCase);

        Assert.True(result.CanRequestReplacementCard);
    }

    [Fact]
    public void EvaluateCase_UnparseableCardStatus_Denied()
    {
        var evaluator = Create(DcSettings());
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, "NOT_A_REAL_STATUS");

        var result = evaluator.EvaluateCase(summerEbtCase);

        Assert.False(result.CanRequestReplacementCard);
        Assert.Equal("dashboard.cardReplacementDisabled", result.CardReplacementDeniedMessageKey);
    }

    [Fact]
    public void EvaluateCase_NullCardStatus_Denied()
    {
        var evaluator = Create(DcSettings());
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, null);

        var result = evaluator.EvaluateCase(summerEbtCase);

        Assert.False(result.CanRequestReplacementCard);
        Assert.Equal("dashboard.cardReplacementDisabled", result.CardReplacementDeniedMessageKey);
    }

    [Fact]
    public void EvaluateCase_EmptyAllowedCardStatuses_AnyStatusAllowed()
    {
        // When AllowedCardStatuses is empty and the issuance type is enabled,
        // the action should be permitted regardless of card status.
        var settings = new SelfServiceRulesSettings
        {
            CardReplacement = new ActionRuleSettings
            {
                Enabled = true,
                DisabledMessageKey = "dashboard.cardReplacementDisabled",
                ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>
                {
                    [IssuanceType.SummerEbt] = new IssuanceTypeRuleSettings
                    {
                        Enabled = true,
                        AllowedCardStatuses = [] // empty => any status allowed
                    }
                }
            }
        };
        var evaluator = Create(settings);
        var summerEbtCase = MakeCase(IssuanceType.SummerEbt, "Active");

        var result = evaluator.EvaluateCase(summerEbtCase);

        Assert.True(result.CanRequestReplacementCard);
    }

    [Fact]
    public void EvaluateHousehold_EmptyAllowedCardStatuses_AnyStatusAllowed()
    {
        // Same open-list behavior at the household level.
        var settings = new SelfServiceRulesSettings
        {
            AddressUpdate = new ActionRuleSettings
            {
                Enabled = true,
                DisabledMessageKey = "dashboard.addressUpdateDisabled",
                ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>
                {
                    [IssuanceType.SummerEbt] = new IssuanceTypeRuleSettings
                    {
                        Enabled = true,
                        AllowedCardStatuses = [] // empty => any status allowed
                    }
                }
            }
        };
        var evaluator = Create(settings);
        var cases = new[] { MakeCase(IssuanceType.SummerEbt, "Mailed") };

        var result = evaluator.EvaluateHousehold(cases);

        Assert.True(result.CanUpdateAddress);
        Assert.Null(result.AddressUpdateDeniedMessageKey);
    }
}
