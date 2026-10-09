using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Tests.Unit.Core.AppSettings;

/// <summary>
/// Covers the data-annotation rules on settings that AddPortalOptions validates at startup, run through
/// the same validator ValidateDataAnnotations registers.
/// </summary>
public class SettingsDataAnnotationsTests
{
    [Fact]
    public void IdProofingValidity_Default_Succeeds()
    {
        Assert.True(Validate(new IdProofingValiditySettings()).Succeeded);
    }

    // Expiration is IdProofingCompletedAt + ValidityDays, so zero or a negative value makes every
    // completed verification already expired and sends every user back through ID proofing.
    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void IdProofingValidity_NonPositiveValidityDays_Fails(int validityDays)
    {
        var result = Validate(new IdProofingValiditySettings { ValidityDays = validityDays });

        Assert.True(result.Failed);
        Assert.Contains("IdProofingValidity:ValidityDays", result.FailureMessage);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(35)]
    public void AddressValidationData_ZeroOrPositiveMaxStreetAddressLength_Succeeds(int maxLength)
    {
        Assert.True(Validate(new AddressValidationDataSettings { MaxStreetAddressLength = maxLength }).Succeeded);
    }

    [Fact]
    public void AddressValidationData_NegativeMaxStreetAddressLength_Fails()
    {
        var result = Validate(new AddressValidationDataSettings { MaxStreetAddressLength = -1 });

        Assert.True(result.Failed);
        Assert.Contains("AddressValidationData:MaxStreetAddressLength", result.FailureMessage);
    }

    // Base appsettings.json sets ["Email"]; each state overrides with its own list.
    [Fact]
    public void StateHouseholdId_WithPreferredType_Succeeds()
    {
        var settings = new StateHouseholdIdSettings { PreferredHouseholdIdTypes = [PreferredHouseholdIdType.Email] };

        Assert.True(Validate(settings).Succeeded);
    }

    // With no preferred type, no household identifier resolves for any user, so every guardian
    // sees an empty dashboard instead of their household.
    [Fact]
    public void StateHouseholdId_Empty_Fails()
    {
        var result = Validate(new StateHouseholdIdSettings());

        Assert.True(result.Failed);
        Assert.Contains("StateHouseholdId:PreferredHouseholdIdTypes", result.FailureMessage);
    }

    private static ValidateOptionsResult Validate<TOptions>(TOptions settings)
        where TOptions : class =>
        new DataAnnotationValidateOptions<TOptions>(Options.DefaultName).Validate(Options.DefaultName, settings);
}
