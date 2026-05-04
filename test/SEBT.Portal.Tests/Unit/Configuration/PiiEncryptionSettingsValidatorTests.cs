using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Configuration;

public class PiiEncryptionSettingsValidatorTests
{
    private readonly PiiEncryptionSettingsValidator _validator = new();

    [Fact]
    public void Validate_WhenCoherentRing_ReturnsSuccess()
    {
        var options = new PiiEncryptionSettings
        {
            ActiveKeyId = "primary",
            Keys =
            [
                new PiiEncryptionKeySetting
                {
                    KeyId = "primary",
                    KeyMaterialBase64 = "YjJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmI="
                }
            ]
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenActiveKeyMissingFromRing_ReturnsFail()
    {
        var options = new PiiEncryptionSettings
        {
            ActiveKeyId = "missing",
            Keys =
            [
                new PiiEncryptionKeySetting
                {
                    KeyId = "primary",
                    KeyMaterialBase64 = "YjJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmI="
                }
            ]
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains("ActiveKeyId", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenNullOptions_ReturnsFail()
    {
        var result = _validator.Validate(Options.DefaultName, null!);

        Assert.True(result.Failed);
    }
}
