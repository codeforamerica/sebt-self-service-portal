using Microsoft.Extensions.Configuration;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Configuration;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class SelfServiceRulesSettingsValidatorTests
{
    private readonly SelfServiceRulesSettingsValidator _validator = new();

    [Fact]
    public void Validate_ValidDcConfig_ReturnsSuccess()
    {
        var settings = CreateDcSettings();

        var result = _validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_ValidCoConfig_ReturnsSuccess()
    {
        var settings = CreateCoSettings();

        var result = _validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_MinimalConfig_WithEnabledFalse_ReturnsSuccess()
    {
        var settings = new SelfServiceRulesSettings
        {
            AddressUpdate = new ActionRuleSettings { Enabled = false },
            CardReplacement = new ActionRuleSettings { Enabled = false }
        };

        var result = _validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_EnabledAction_WithNoIssuanceTypeRules_ReturnsFailure()
    {
        var settings = new SelfServiceRulesSettings
        {
            AddressUpdate = new ActionRuleSettings
            {
                Enabled = true,
                ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>()
            },
            CardReplacement = new ActionRuleSettings { Enabled = false }
        };

        var result = _validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Contains("AddressUpdate", result.Failures!.Single());
    }

    [Fact]
    public void Validate_EnabledIssuanceType_WithNoAllowedCardStatuses_ReturnsSuccess()
    {
        // Empty AllowedCardStatuses means "any card status is allowed"
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

        var result = _validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void BindConfiguration_DcConfig_BindsCorrectly()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SelfServiceRules:AddressUpdate:Enabled", "true" },
                { "SelfServiceRules:AddressUpdate:DisabledMessageKey", "addressUpdateDisabled" },
                { "SelfServiceRules:AddressUpdate:ByIssuanceType:SummerEbt:Enabled", "true" },
                { "SelfServiceRules:AddressUpdate:ByIssuanceType:SummerEbt:AllowedCardStatuses:0", "Active" },
                { "SelfServiceRules:AddressUpdate:ByIssuanceType:SummerEbt:AllowedCardStatuses:1", "Mailed" },
                { "SelfServiceRules:AddressUpdate:ByIssuanceType:TanfEbtCard:Enabled", "false" },
                { "SelfServiceRules:AddressUpdate:ByIssuanceType:SnapEbtCard:Enabled", "false" },
                { "SelfServiceRules:CardReplacement:Enabled", "true" },
                { "SelfServiceRules:CardReplacement:ByIssuanceType:SummerEbt:Enabled", "true" },
                { "SelfServiceRules:CardReplacement:ByIssuanceType:SummerEbt:AllowedCardStatuses:0", "Lost" },
                { "SelfServiceRules:CardReplacement:ByIssuanceType:SummerEbt:AllowedCardStatuses:1", "Stolen" },
                { "SelfServiceRules:CardReplacement:ByIssuanceType:SummerEbt:AllowedCardStatuses:2", "Damaged" }
            })
            .Build();

        var settings = new SelfServiceRulesSettings();
        config.GetSection(SelfServiceRulesSettings.SectionName).Bind(settings);

        Assert.True(settings.AddressUpdate.Enabled);
        Assert.Equal("addressUpdateDisabled", settings.AddressUpdate.DisabledMessageKey);
        Assert.True(settings.AddressUpdate.ByIssuanceType[IssuanceType.SummerEbt].Enabled);
        Assert.Contains(CardStatus.Active, settings.AddressUpdate.ByIssuanceType[IssuanceType.SummerEbt].AllowedCardStatuses);
        Assert.Contains(CardStatus.Mailed, settings.AddressUpdate.ByIssuanceType[IssuanceType.SummerEbt].AllowedCardStatuses);
        Assert.False(settings.AddressUpdate.ByIssuanceType[IssuanceType.TanfEbtCard].Enabled);

        Assert.True(settings.CardReplacement.Enabled);
        Assert.Contains(CardStatus.Lost, settings.CardReplacement.ByIssuanceType[IssuanceType.SummerEbt].AllowedCardStatuses);
        Assert.Contains(CardStatus.Stolen, settings.CardReplacement.ByIssuanceType[IssuanceType.SummerEbt].AllowedCardStatuses);
        Assert.Contains(CardStatus.Damaged, settings.CardReplacement.ByIssuanceType[IssuanceType.SummerEbt].AllowedCardStatuses);
    }

    private static SelfServiceRulesSettings CreateDcSettings() => new()
    {
        AddressUpdate = new ActionRuleSettings
        {
            Enabled = true,
            DisabledMessageKey = "actionNavigationSelfServiceUnavailable",
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
            DisabledMessageKey = "actionNavigationSelfServiceUnavailable",
            ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>
            {
                [IssuanceType.SummerEbt] = new() { Enabled = true, AllowedCardStatuses = [CardStatus.Lost, CardStatus.Stolen, CardStatus.Damaged] },
                [IssuanceType.TanfEbtCard] = new() { Enabled = false },
                [IssuanceType.SnapEbtCard] = new() { Enabled = false },
                [IssuanceType.Unknown] = new() { Enabled = false }
            }
        }
    };

    private static SelfServiceRulesSettings CreateCoSettings() => new()
    {
        AddressUpdate = new ActionRuleSettings { Enabled = false },
        CardReplacement = new ActionRuleSettings { Enabled = false }
    };
}
