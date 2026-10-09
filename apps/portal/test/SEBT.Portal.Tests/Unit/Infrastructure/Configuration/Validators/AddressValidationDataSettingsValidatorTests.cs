using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration.Validators;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Configuration.Validators;

public class AddressValidationDataSettingsValidatorTests
{
    private readonly AddressValidationDataSettingsValidator _validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WhenBlockedAddressFileNotSet_Succeeds(string? blockedAddressFile)
    {
        var result = _validator.Validate(null, new AddressValidationDataSettings { BlockedAddressFile = blockedAddressFile });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenBlockedAddressFileIsEmbedded_Succeeds()
    {
        var result = _validator.Validate(
            null, new AddressValidationDataSettings { BlockedAddressFile = "co-undeliverable-addresses.csv" });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenBlockedAddressFileIsNotEmbedded_Fails()
    {
        var result = _validator.Validate(
            null, new AddressValidationDataSettings { BlockedAddressFile = "no-such-file.csv" });

        Assert.True(result.Failed);
        Assert.Contains("AddressValidationData:BlockedAddressFile", result.FailureMessage);
        Assert.Contains("no-such-file.csv", result.FailureMessage);
    }
}
