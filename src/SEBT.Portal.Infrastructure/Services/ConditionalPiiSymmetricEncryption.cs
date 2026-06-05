using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Gates <see cref="PiiAesGcmSymmetricEncryption"/> behind <see cref="PiiEncryptionSettings.EncryptAtRest"/> for writes while
/// preserving envelope decryption and legacy plaintext pass-through on reads.
/// </summary>
public sealed class ConditionalPiiSymmetricEncryption : IPiiSymmetricEncryption
{
    private readonly PiiAesGcmSymmetricEncryption _inner;
    private readonly PiiEncryptionSettings _settings;

    public ConditionalPiiSymmetricEncryption(
        PiiAesGcmSymmetricEncryption inner,
        IOptions<PiiEncryptionSettings> options)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        ArgumentNullException.ThrowIfNull(options);
        _settings = options.Value ?? throw new InvalidOperationException("PiiEncryption settings are missing.");
    }

    public bool IsEnvelope(string? storedValue) => _inner.IsEnvelope(storedValue);

    public string? Encrypt(string? plaintext)
    {
        if (_settings.EncryptAtRest)
        {
            return _inner.Encrypt(plaintext);
        }

        return StorePlaintextForColumn(plaintext);
    }

    public string Decrypt(string storedValue) => _inner.Decrypt(storedValue);

    public string? DecryptOrPassThroughLegacy(string? storedValue) =>
        _inner.DecryptOrPassThroughLegacy(storedValue);

    public string ReSealWithActiveEncryptor(string envelopeCiphertext)
    {
        if (_settings.EncryptAtRest)
        {
            return _inner.ReSealWithActiveEncryptor(envelopeCiphertext);
        }

        var plaintext = _inner.DecryptOrPassThroughLegacy(envelopeCiphertext);
        if (string.IsNullOrEmpty(plaintext))
        {
            throw new PiiDecryptException(
                "PII envelope round-trip decryption produced empty payloads while EncryptAtRest is disabled.");
        }

        return plaintext;
    }

    private static string? StorePlaintextForColumn(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return null;
        }

        var trimmed = plaintext.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
