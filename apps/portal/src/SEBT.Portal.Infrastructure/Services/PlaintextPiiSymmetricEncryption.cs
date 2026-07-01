using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Plaintext-at-rest storage when <see cref="Core.AppSettings.PiiEncryptionSettings.EncryptAtRest"/> is false and no key ring is configured.
/// Does not decrypt existing AES-GCM envelopes — configure keys or enable encryption before reading ciphertext columns.
/// </summary>
public sealed class PlaintextPiiSymmetricEncryption : IPiiSymmetricEncryption
{
    private const string EnvelopeWithoutKeysMessage =
        "PII ciphertext is stored in the database but PiiEncryption keys are not configured. " +
        "Configure PiiEncryption:ActiveKeyId and Keys, or ensure EncryptAtRest is false only before any ciphertext is written.";

    public bool IsEnvelope(string? storedValue) =>
        !string.IsNullOrEmpty(storedValue)
        && storedValue.StartsWith(PiiAesGcmSymmetricEncryption.EnvelopePrefix, StringComparison.Ordinal);

    public string? Encrypt(string? plaintext) => PiiPlaintextColumnStorage.StorePlaintextForColumn(plaintext);

    public string Decrypt(string storedValue)
    {
        if (IsEnvelope(storedValue))
        {
            throw new PiiDecryptException(EnvelopeWithoutKeysMessage);
        }

        return storedValue.Trim();
    }

    public string? DecryptOrPassThroughLegacy(string? storedValue)
    {
        if (string.IsNullOrEmpty(storedValue))
        {
            return storedValue;
        }

        if (IsEnvelope(storedValue))
        {
            throw new PiiDecryptException(EnvelopeWithoutKeysMessage);
        }

        return storedValue.Trim();
    }

    public string ReSealWithActiveEncryptor(string envelopeCiphertext) =>
        Decrypt(envelopeCiphertext);
}
