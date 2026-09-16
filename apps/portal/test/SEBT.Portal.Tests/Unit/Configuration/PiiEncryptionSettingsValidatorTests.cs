using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Configuration;
using SEBT.Portal.Infrastructure.Configuration.Validators;
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
            EncryptAtRest = true,
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
    public void Validate_WhenKeyMaterialNot256Bits_ReturnsFail()
    {
        var options = new PiiEncryptionSettings
        {
            EncryptAtRest = true,
            ActiveKeyId = "short-key",
            Keys =
            [
                new PiiEncryptionKeySetting
                {
                    KeyId = "short-key",
                    // Decodes to 16 bytes — AES-128-sized; only 256-bit keys are allowed.
                    KeyMaterialBase64 = Convert.ToBase64String(new byte[16])
                }
            ]
        };

        var result = _validator.Validate(Options.DefaultName, options);

        Assert.True(result.Failed);
        Assert.Contains("256-bit", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("32", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenActiveKeyMissingFromRing_ReturnsFail()
    {
        var options = new PiiEncryptionSettings
        {
            EncryptAtRest = true,
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

    [Fact]
    public void Validate_WhenEncryptAtRestFalseAndNoKeys_ReturnsSuccess()
    {
        var result = _validator.Validate(
            Options.DefaultName,
            new PiiEncryptionSettings { EncryptAtRest = false });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenEncryptAtRestTrueAndNoKeys_ReturnsFail()
    {
        var result = _validator.Validate(
            Options.DefaultName,
            new PiiEncryptionSettings { EncryptAtRest = true });

        Assert.True(result.Failed);
        Assert.Contains("EncryptAtRest", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_WhenRunStartupBackfillTrueAndEncryptAtRestFalse_ReturnsFail()
    {
        var result = _validator.Validate(
            Options.DefaultName,
            new PiiEncryptionSettings
            {
                EncryptAtRest = false,
                RunStartupBackfill = true
            });

        Assert.True(result.Failed);
        Assert.Contains("RunStartupBackfill", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("EncryptAtRest", result.FailureMessage, StringComparison.Ordinal);
    }
}
