using Microsoft.Extensions.Configuration;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Infrastructure.Configuration;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class SelfServiceRulesSettingsValidatorTests
{
    private readonly SelfServiceRulesSettingsValidator _validator = new();

    private static SelfServiceRulesSettings CreateDcConfig() => new()
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

    private static SelfServiceRulesSettings CreateCoConfig() => new()
    {
        AddressUpdate = new ActionRuleSettings
        {
            Enabled = false,
            DisabledMessageKey = "actionNavigationSelfServiceUnavailable",
            ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>()
        },
        CardReplacement = new ActionRuleSettings
        {
            Enabled = false,
            DisabledMessageKey = "actionNavigationSelfServiceUnavailable",
            ByIssuanceType = new Dictionary<IssuanceType, IssuanceTypeRuleSettings>()
        }
    };

    // --- Valid configs ---

    [Fact]
    public void Validate_WithValidDcConfig_ReturnsSuccess()
    {
        var result = _validator.Validate(null, CreateDcConfig());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WithValidCoConfig_BothActionsDisabled_ReturnsSuccess()
    {
        var result = _validator.Validate(null, CreateCoConfig());

        Assert.True(result.Succeeded);
    }

    // --- Enabled action with no issuance type rules ---

    [Fact]
    public void Validate_AddressUpdateEnabled_WithNoIssuanceTypeRules_ReturnsFailure()
    {
        var settings = CreateDcConfig();
        settings.AddressUpdate.ByIssuanceType.Clear();

        var result = _validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Contains("AddressUpdate", result.Failures!.Single());
    }

    [Fact]
    public void Validate_CardReplacementEnabled_WithNoIssuanceTypeRules_ReturnsFailure()
    {
        var settings = CreateDcConfig();
        settings.CardReplacement.ByIssuanceType.Clear();

        var result = _validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Contains("CardReplacement", result.Failures!.Single());
    }

    [Fact]
    public void Validate_BothActionsEnabled_WithNoIssuanceTypeRules_ReturnsBothFailures()
    {
        var settings = CreateDcConfig();
        settings.AddressUpdate.ByIssuanceType.Clear();
        settings.CardReplacement.ByIssuanceType.Clear();

        var result = _validator.Validate(null, settings);

        Assert.False(result.Succeeded);
        Assert.Equal(2, result.Failures!.Count());
    }

    // --- Empty AllowedCardStatuses is valid ---

    [Fact]
    public void Validate_EnabledIssuanceType_WithEmptyAllowedCardStatuses_ReturnsSuccess()
    {
        var settings = CreateDcConfig();
        settings.AddressUpdate.ByIssuanceType[IssuanceType.SummerEbt] =
            new IssuanceTypeRuleSettings { Enabled = true, AllowedCardStatuses = [] };

        var result = _validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    // --- Config binding ---

    [Fact]
    public void BindConfiguration_WithValidJson_BindsCorrectly()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "SelfServiceRules:AddressUpdate:Enabled", "true" },
                { "SelfServiceRules:AddressUpdate:DisabledMessageKey", "actionNavigationSelfServiceUnavailable" },
                { "SelfServiceRules:AddressUpdate:ByIssuanceType:SummerEbt:Enabled", "true" },
                { "SelfServiceRules:AddressUpdate:ByIssuanceType:SummerEbt:AllowedCardStatuses:0", "Active" },
                { "SelfServiceRules:AddressUpdate:ByIssuanceType:SummerEbt:AllowedCardStatuses:1", "Mailed" },
                { "SelfServiceRules:AddressUpdate:ByIssuanceType:TanfEbtCard:Enabled", "false" },
                { "SelfServiceRules:CardReplacement:Enabled", "false" },
                { "SelfServiceRules:CardReplacement:ByIssuanceType:SummerEbt:Enabled", "false" }
            })
            .Build();

        var settings = new SelfServiceRulesSettings();
        config.GetSection(SelfServiceRulesSettings.SectionName).Bind(settings);

        Assert.True(settings.AddressUpdate.Enabled);
        Assert.Equal("actionNavigationSelfServiceUnavailable", settings.AddressUpdate.DisabledMessageKey);
        Assert.True(settings.AddressUpdate.ByIssuanceType[IssuanceType.SummerEbt].Enabled);
        Assert.Equal([CardStatus.Active, CardStatus.Mailed], settings.AddressUpdate.ByIssuanceType[IssuanceType.SummerEbt].AllowedCardStatuses);
        Assert.False(settings.AddressUpdate.ByIssuanceType[IssuanceType.TanfEbtCard].Enabled);
        Assert.False(settings.CardReplacement.Enabled);
    }
}
