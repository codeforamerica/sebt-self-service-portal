using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Tests.Unit.TestSupport;

/// <summary>
/// Shared reversible PII + email-hash services for repositories and seed integration tests (deterministic AES-256-GCM keys).
/// </summary>
public static class TestPortalCryptography
{
    public static readonly string TestIdentifierHasherSecretKey = "TestKeyMustBeAtLeast32CharactersLong!!";

    public static readonly IOptions<PiiEncryptionSettings> PiiOptions = Options.Create(
        new PiiEncryptionSettings
        {
            EncryptAtRest = true,
            ActiveKeyId = "test-primary",
            Keys =
            [
                new PiiEncryptionKeySetting
                {
                    KeyId = "test-primary",
                    KeyMaterialBase64 =
                        // 32 repetitions of ASCII 'b' — decoded length must be exactly 256 bits for AES-256-GCM key.
                        "YmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmJiYmI="
                }
            ]
        });

    public static readonly IOptions<IdentifierHasherSettings> IdentifierOptions =
        Options.Create(new IdentifierHasherSettings { SecretKey = TestIdentifierHasherSecretKey });

    public static readonly IPiiSymmetricEncryption PiiSymmetricEncryption =
        new PiiAesGcmSymmetricEncryption(PiiOptions);

    public static readonly IEmailLookupHasher EmailLookupHasher =
        new EmailLookupHasher(IdentifierOptions);

    public static string NormalizeEmailStrict(string plaintext) =>
        EmailNormalizer.Normalize(plaintext);

    public static string FingerprintEmail(string anyCasePlaintextEmail) =>
        EmailLookupHasher.HashNormalized(NormalizeEmailStrict(anyCasePlaintextEmail))!;

    public static string StoredEmailPlaintext(string? ciphertext) =>
        string.IsNullOrEmpty(ciphertext)
            ? ""
            : PiiSymmetricEncryption.DecryptOrPassThroughLegacy(ciphertext)!;
}
