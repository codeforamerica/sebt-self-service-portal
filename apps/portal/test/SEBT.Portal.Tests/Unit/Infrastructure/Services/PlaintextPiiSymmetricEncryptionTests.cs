using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Infrastructure.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class PlaintextPiiSymmetricEncryptionTests
{
    private static readonly PlaintextPiiSymmetricEncryption Crypto = new();

    [Fact]
    public void Encrypt_StoresTrimmedPlaintext()
    {
        var stored = Crypto.Encrypt("  user@example.com  ");
        Assert.False(Crypto.IsEnvelope(stored));
        Assert.Equal("user@example.com", stored);
    }

    [Fact]
    public void DecryptOrPassThroughLegacy_ReturnsTrimmedPlaintext()
    {
        Assert.Equal("user@example.com", Crypto.DecryptOrPassThroughLegacy("  user@example.com  "));
    }

    [Fact]
    public void DecryptOrPassThroughLegacy_WhenEnvelopePresent_Throws()
    {
        var envelope = new PiiAesGcmSymmetricEncryption(Options.Create(
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
            })).Encrypt("sealed@example.com");

        var ex = Assert.Throws<PiiDecryptException>(() => Crypto.DecryptOrPassThroughLegacy(envelope));
        Assert.Contains("keys are not configured", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
