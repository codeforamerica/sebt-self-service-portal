using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Infrastructure.Services;
using Xunit;

namespace SEBT.Portal.Tests.Unit.Infrastructure.Services;

public class PiiAesGcmSymmetricEncryptionTests
{
    private static PiiAesGcmSymmetricEncryption CreateEncryption(
        string activeKeyId,
        params (string KeyId, string Base64Material)[] keys)
    {
        var settings = new PiiEncryptionSettings
        {
            ActiveKeyId = activeKeyId,
            Keys = keys.Select(k => new PiiEncryptionKeySetting
            {
                KeyId = k.KeyId,
                KeyMaterialBase64 = k.Base64Material
            }).ToList()
        };

        return new PiiAesGcmSymmetricEncryption(Options.Create(settings));
    }

    [Fact]
    public void EncryptThenDecrypt_roundTrips_utf8_plaintext()
    {
        var enc = CreateEncryption("a", ("a", "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWE="));
        var cipher = enc.Encrypt("  parent+child@example.com");
        Assert.True(enc.IsEnvelope(cipher));
        Assert.Equal("parent+child@example.com", enc.DecryptOrPassThroughLegacy(cipher));
    }

    [Fact]
    public void ReSealWithActiveEncryptor_decryptsUsingEmbeddedKeyId_thenRewrapsWithActiveKey()
    {
        var keyA = "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWE="; // 32 x 'a'
        var keyB = "YmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmI="; // 32 x 'b'

        var writerA = CreateEncryption("key-a", ("key-a", keyA), ("key-b", keyB));
        var cipher = writerA.Encrypt("rotate-me@example.com")!;

        var writerB = CreateEncryption("key-b", ("key-a", keyA), ("key-b", keyB));
        var resealed = writerB.ReSealWithActiveEncryptor(cipher);

        Assert.NotEqual(cipher, resealed);
        Assert.Equal("rotate-me@example.com", writerB.DecryptOrPassThroughLegacy(resealed));
    }

    [Fact]
    public void Decrypt_whenTagIsTampered_throws_PiiDecryptException()
    {
        var enc = CreateEncryption("k", ("k", "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWE="));
        var cipher = enc.Encrypt("victim@example.com")!;

        // Flip a stable position inside the Base64 payload (not the ASCII prefix).
        var i = cipher.Length - 4;
        var tampered = cipher[..i] + (cipher[i] == 'A' ? 'B' : 'A') + cipher[(i + 1)..];

        var ex = Assert.Throws<PiiDecryptException>(() => enc.Decrypt(tampered));
        Assert.NotNull(ex.InnerException);
    }
}
