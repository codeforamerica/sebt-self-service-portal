using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Tests.Integration.StartupValidation;

/// <summary>
/// Proves the app refuses to start when the card replacement cooldown is out of range, and that
/// the shipped configuration binds the 14-day default. The CO state file does not define the key,
/// so an environment variable set here is not overridden. The missing-key case stays at unit level
/// in SelfServiceRulesSettingsValidatorTests: an environment variable cannot unset a key the base
/// appsettings.json defines.
/// </summary>
[Collection("Integration")]
[Trait("Category", "Integration")]
public class SelfServiceRulesStartupValidationTests : StartupValidationTestBase
{
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("366")]
    public void Startup_WithCooldownDaysOutOfRange_ThrowsOptionsValidationException(string cooldownDays)
    {
        SetEnv("SelfServiceRules__CardReplacement__CooldownDays", cooldownDays);

        using var factory = CreateFactory();
        var ex = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());

        Assert.Contains("CardReplacement:CooldownDays must be between", ex.Message);
    }

    [Fact]
    public void Startup_WithShippedConfig_BindsFourteenDayCooldown()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var settings = factory.Services
            .GetRequiredService<IOptionsMonitor<SelfServiceRulesSettings>>()
            .CurrentValue;

        Assert.Equal(14, settings.CardReplacement.CooldownDays);
        Assert.Null(settings.AddressUpdate.CooldownDays);
    }
}
