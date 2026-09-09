using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Infrastructure.Services;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class PiiSymmetricEncryptionFactoryTests
{
    [Fact]
    public void Create_WhenEncryptAtRestFalseAndNoKeys_ReturnsPlaintextImplementation()
    {
        var crypto = PiiSymmetricEncryptionFactory.Create(Options.Create(
            new PiiEncryptionSettings { EncryptAtRest = false }));

        Assert.IsType<PlaintextPiiSymmetricEncryption>(crypto);
        Assert.Equal("plain@example.com", crypto.Encrypt("plain@example.com"));
    }

    [Fact]
    public void Create_WhenEncryptAtRestTrueAndNoKeys_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PiiSymmetricEncryptionFactory.Create(Options.Create(
                new PiiEncryptionSettings { EncryptAtRest = true })));
    }

    [Fact]
    public void Create_WhenEncryptAtRestTrueWithKeys_ReturnsConditionalWrapper()
    {
        var crypto = PiiSymmetricEncryptionFactory.Create(Options.Create(
            new PiiEncryptionSettings
            {
                EncryptAtRest = true,
                ActiveKeyId = "primary",
                Keys =
                [
                    new PiiEncryptionKeySetting
                    {
                        KeyId = "primary",
                        KeyMaterialBase64 = "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWE="
                    }
                ]
            }));

        Assert.IsType<ConditionalPiiSymmetricEncryption>(crypto);
        Assert.True(crypto.IsEnvelope(crypto.Encrypt("user@example.com")));
    }

    [Fact]
    public void Create_WhenEncryptAtRestFalseWithKeys_StillReadsEnvelopes()
    {
        var withKeys = new PiiEncryptionSettings
        {
            EncryptAtRest = true,
            ActiveKeyId = "primary",
            Keys =
            [
                new PiiEncryptionKeySetting
                {
                    KeyId = "primary",
                    KeyMaterialBase64 = "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWE="
                }
            ]
        };

        var encrypting = PiiSymmetricEncryptionFactory.Create(Options.Create(withKeys));
        var envelope = encrypting.Encrypt("legacy@example.com");

        var plaintextMode = PiiSymmetricEncryptionFactory.Create(Options.Create(
            new PiiEncryptionSettings
            {
                EncryptAtRest = false,
                ActiveKeyId = withKeys.ActiveKeyId,
                Keys = withKeys.Keys
            }));

        Assert.IsType<ConditionalPiiSymmetricEncryption>(plaintextMode);
        Assert.Equal("legacy@example.com", plaintextMode.DecryptOrPassThroughLegacy(envelope));
        Assert.False(plaintextMode.IsEnvelope(plaintextMode.Encrypt("legacy@example.com")));
    }
}
