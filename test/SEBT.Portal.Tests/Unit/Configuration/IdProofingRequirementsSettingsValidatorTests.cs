using Microsoft.Extensions.Configuration;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Infrastructure.Configuration;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class IdProofingRequirementsSettingsValidatorTests
{
    private readonly IdProofingRequirementsSettingsValidator _validator = new();

    [Fact]
    public void Validate_WhenOptionsValid_ReturnsSuccess()
    {
        var options = new IdProofingRequirementsSettings
        {
            AddressView = IalLevel.IAL1plus,
            EmailView = IalLevel.IAL1,
            PhoneView = IalLevel.IAL1
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenOptionsNull_ReturnsFailure()
    {
        var result = _validator.Validate(null, null!);

        Assert.False(result.Succeeded);
        Assert.Contains("null", result.Failures!.Single());
    }

    [Fact]
    public void BindConfiguration_WhenFeatureBasedKeysProvided_BindsCorrectly()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "IdProofingRequirements:address+view", "IAL1plus" },
                { "IdProofingRequirements:email+view", "IAL1" },
                { "IdProofingRequirements:phone+view", "IAL2" }
            })
            .Build();

        var settings = new IdProofingRequirementsSettings();
        config.GetSection(IdProofingRequirementsSettings.SectionName).Bind(settings);

        // Assert
        Assert.Equal(IalLevel.IAL1plus, settings.AddressView);
        Assert.Equal(IalLevel.IAL1, settings.EmailView);
        Assert.Equal(IalLevel.IAL2, settings.PhoneView);
    }
}
