namespace SEBT.Portal.Core.Services;

/// <summary>
/// Encrypts/decrypts short UTF-8 strings at rest using an authenticated envelope bound to an explicit key id.
/// </summary>
public interface IPiiSymmetricEncryption
{
    /// <summary>
    /// True when the stored value was produced by <see cref="Encrypt"/> (case-sensitive prefix).
    /// </summary>
    bool IsEnvelope(string? storedValue);

    /// <summary>Returns null when <paramref name="plaintext"/> is null or empty.</summary>
    string? Encrypt(string? plaintext);

    /// <summary>
    /// Requires a valid authenticated envelope produced by this service — throws <see cref="Exceptions.PiiDecryptException"/> on mismatch.
    /// </summary>
    string Decrypt(string storedValue);

    /// <summary>
    /// Converts stored column text to plaintext. Envelopes decrypt; non-envelope values are returned verbatim (migration / legacy plaintext).
    /// Throws <see cref="Exceptions.PiiDecryptException"/> for envelopes that decrypt but fail AEAD verification.
    /// </summary>
    string? DecryptOrPassThroughLegacy(string? storedValue);

    /// <summary>
    /// Decrypts ciphertext (<see cref="IsEnvelope"/> true), then seals it again using the configured active encryptor (key rotation / re-pack helper).
    /// </summary>
    string ReSealWithActiveEncryptor(string envelopeCiphertext);
}
