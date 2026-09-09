using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class ConditionalPiiSymmetricEncryptionTests
{
    private static readonly string KeyMaterial =
        "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWE=";

    private static ConditionalPiiSymmetricEncryption Create(bool encryptAtRest)
    {
        var settings = new PiiEncryptionSettings
        {
            EncryptAtRest = encryptAtRest,
            ActiveKeyId = "primary",
            Keys =
            [
                new PiiEncryptionKeySetting
                {
                    KeyId = "primary",
                    KeyMaterialBase64 = KeyMaterial
                }
            ]
        };

        var inner = new PiiAesGcmSymmetricEncryption(Options.Create(settings));
        return new ConditionalPiiSymmetricEncryption(inner, Options.Create(settings));
    }

    [Fact]
    public void Encrypt_WhenEncryptAtRestTrue_ProducesEnvelope()
    {
        var crypto = Create(encryptAtRest: true);
        var cipher = crypto.Encrypt("  user@example.com  ");
        Assert.True(crypto.IsEnvelope(cipher));
        Assert.Equal("user@example.com", crypto.DecryptOrPassThroughLegacy(cipher));
    }

    [Fact]
    public void Encrypt_WhenEncryptAtRestFalse_StoresTrimmedPlaintext()
    {
        var crypto = Create(encryptAtRest: false);
        var stored = crypto.Encrypt("  user@example.com  ");
        Assert.False(crypto.IsEnvelope(stored));
        Assert.Equal("user@example.com", stored);
        Assert.Equal("user@example.com", crypto.DecryptOrPassThroughLegacy(stored));
    }

    [Fact]
    public void DecryptOrPassThroughLegacy_WhenEncryptAtRestFalse_StillDecryptsExistingEnvelope()
    {
        var encrypting = Create(encryptAtRest: true);
        var envelope = encrypting.Encrypt("legacy-upgrade@example.com");

        var plaintextWrites = Create(encryptAtRest: false);
        Assert.Equal("legacy-upgrade@example.com", plaintextWrites.DecryptOrPassThroughLegacy(envelope));
    }

    [Fact]
    public void ReSealWithActiveEncryptor_WhenEncryptAtRestFalse_ReturnsPlaintext()
    {
        var encrypting = Create(encryptAtRest: true);
        var envelope = encrypting.Encrypt("rotate-me@example.com")!;

        var plaintextWrites = Create(encryptAtRest: false);
        var result = plaintextWrites.ReSealWithActiveEncryptor(envelope);
        Assert.False(plaintextWrites.IsEnvelope(result));
        Assert.Equal("rotate-me@example.com", result);
    }
}
