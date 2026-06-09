using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Selects reversible PII crypto: AES-GCM when <see cref="PiiEncryptionSettings.EncryptAtRest"/> (requires keys),
/// optional AES read support when encryption is off but keys remain configured, or plaintext-only when off and keyless.
/// </summary>
public static class PiiSymmetricEncryptionFactory
{
    public static IPiiSymmetricEncryption Create(IOptions<PiiEncryptionSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var settings = options.Value ?? throw new InvalidOperationException("PiiEncryption settings are missing.");

        if (!settings.HasKeyRingConfiguration())
        {
            if (settings.EncryptAtRest)
            {
                throw new InvalidOperationException(
                    "PiiEncryption:ActiveKeyId and PiiEncryption:Keys are required when EncryptAtRest is true.");
            }

            return new PlaintextPiiSymmetricEncryption();
        }

        var inner = new PiiAesGcmSymmetricEncryption(options);
        return new ConditionalPiiSymmetricEncryption(inner, options);
    }
}
