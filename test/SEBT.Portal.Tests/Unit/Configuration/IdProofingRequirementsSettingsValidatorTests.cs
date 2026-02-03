using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class IdProofingRequirementsSettingsValidatorTests
{
    private readonly IdProofingRequirementsSettingsValidator _validator = new();

    [Fact]
    public void Validate_WhenAllValuesValid_ReturnsSuccess()
    {
        var options = new IdProofingRequirementsSettings
        {
            Address = "IAL1",
            Email = "None",
            Phone = "None"
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenValuesValidCaseInsensitive_ReturnsSuccess()
    {
        var options = new IdProofingRequirementsSettings
        {
            Address = "ial1",
            Email = "none",
            Phone = "IAL1"
        };

        var result = _validator.Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenAddressInvalid_ReturnsFailure()
    {
        var options = new IdProofingRequirementsSettings
        {
            Address = "IAL2",
            Email = "None",
            Phone = "None"
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        var failure = result.Failures!.Single();
        Assert.Contains("Address", failure);
        Assert.Contains("IAL2", failure);
    }

    [Fact]
    public void Validate_WhenEmailInvalid_ReturnsFailure()
    {
        var options = new IdProofingRequirementsSettings
        {
            Address = "IAL1",
            Email = "iall", // to represent common typos
            Phone = "None"
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains("Email", result.Failures!.Single());
    }

    [Fact]
    public void Validate_WhenPhoneEmpty_ReturnsFailure()
    {
        var options = new IdProofingRequirementsSettings
        {
            Address = "IAL1",
            Email = "None",
            Phone = ""
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Contains("Phone", result.Failures!.Single());
    }

    [Fact]
    public void Validate_WhenOptionsNull_ReturnsFailure()
    {
        var result = _validator.Validate(null, null!);

        Assert.False(result.Succeeded);
        Assert.Contains("null", result.Failures!.Single());
    }

    [Fact]
    public void Validate_WhenMultipleInvalid_ReturnsAllFailures()
    {
        var options = new IdProofingRequirementsSettings
        {
            Address = "Invalid",
            Email = "Bad",
            Phone = "Whatevenisthis!!1"
        };

        var result = _validator.Validate(null, options);

        Assert.False(result.Succeeded);
        Assert.Equal(3, result.Failures!.Count());
    }
}
