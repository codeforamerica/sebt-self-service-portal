using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration.Validators;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Configuration.Validators;

public class EnrollmentCheckerSettingsValidatorTests
{
    private readonly EnrollmentCheckerSettingsValidator _validator = new();

    // Zeroed income figures are the shipped default and mean "not configured": the features
    // endpoint omits them. States that do not screen income must keep booting.
    [Fact]
    public void Validate_DefaultSettings_Succeeds()
    {
        var result = _validator.Validate(null, new EnrollmentCheckerSettings());

        Assert.True(result.Succeeded);
    }

    // The figures appsettings.dc.example.json ships.
    [Fact]
    public void Validate_ConfiguredIncomeEligibility_Succeeds()
    {
        var settings = WithIncome(baseThreshold: 28953, perMemberIncrement: 10175, maxHouseholdSize: 8);

        var result = _validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    // A flat threshold is a valid configuration; IncomeEligibilitySettings.IsConfigured excludes the increment.
    [Fact]
    public void Validate_FlatThresholdWithoutIncrement_Succeeds()
    {
        var settings = WithIncome(baseThreshold: 28953, perMemberIncrement: 0, maxHouseholdSize: 8);

        var result = _validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(-1, 0, 0, "BaseThreshold")]
    [InlineData(28953, -1, 8, "PerMemberIncrement")]
    [InlineData(28953, 10175, -1, "MaxHouseholdSize")]
    public void Validate_NegativeIncomeFigure_Fails(
        int baseThreshold, int perMemberIncrement, int maxHouseholdSize, string property)
    {
        var settings = WithIncome(baseThreshold, perMemberIncrement, maxHouseholdSize);

        var result = _validator.Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains($"EnrollmentChecker:IncomeEligibility:{property}", result.FailureMessage);
    }

    // With only one of the pair set, the endpoint treats the section as unconfigured and silently
    // drops income screening, so a half-filled section is a typo rather than an intent.
    [Theory]
    [InlineData(28953, 0)]
    [InlineData(0, 8)]
    public void Validate_ThresholdAndHouseholdSizeNotSetTogether_Fails(int baseThreshold, int maxHouseholdSize)
    {
        var settings = WithIncome(baseThreshold, perMemberIncrement: 0, maxHouseholdSize);

        var result = _validator.Validate(null, settings);

        Assert.True(result.Failed);
        Assert.Contains("BaseThreshold and", result.FailureMessage);
    }

    // Deliberately not a rule. This section hot-reloads from AppConfig, and a rejected reload fails
    // the whole checker features endpoint until corrected; blank banner copy is not worth that.
    [Fact]
    public void Validate_BlankMaintenanceBannerMessage_Succeeds()
    {
        var settings = new EnrollmentCheckerSettings();
        settings.MaintenanceBanner.Message["en"] = "   ";

        var result = _validator.Validate(null, settings);

        Assert.True(result.Succeeded);
    }

    private static EnrollmentCheckerSettings WithIncome(
        decimal baseThreshold, decimal perMemberIncrement, int maxHouseholdSize) =>
        new()
        {
            IncomeEligibility = new IncomeEligibilitySettings
            {
                BaseThreshold = baseThreshold,
                PerMemberIncrement = perMemberIncrement,
                MaxHouseholdSize = maxHouseholdSize
            }
        };
}
