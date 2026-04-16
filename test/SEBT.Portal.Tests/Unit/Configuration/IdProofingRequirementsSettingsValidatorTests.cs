using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class IdProofingRequirementsSettingsValidatorTests
{
    private readonly IdProofingRequirementsSettingsValidator _validator = new();

    [Fact]
    public void Validate_WhenOptionsValid_ReturnsSuccess()
    {
        var options = new IdProofingRequirementsSettings();

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
}
