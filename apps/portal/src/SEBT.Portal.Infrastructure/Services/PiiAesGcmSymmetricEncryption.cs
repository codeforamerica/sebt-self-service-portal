using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// AEAD envelopes for reversible PII: AES-GCM / 96-bit nonce / 128-bit tag. Format is stable + versioned (<see cref="EnvelopePrefix"/>).
/// </summary>
public sealed class PiiAesGcmSymmetricEncryption : IPiiSymmetricEncryption
{
    public const byte EnvelopeVersion = 1;
    public const string EnvelopePrefix = "sep-pii:v1:";

    private const int TagSizeBytes = 16;
    private const int NonceSizeBytes = 12;

    private readonly IReadOnlyDictionary<string, byte[]> _keys;
    private readonly string _encryptKeyId;
    private readonly byte[] _encryptKeyRaw;

    public PiiAesGcmSymmetricEncryption(IOptions<PiiEncryptionSettings> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var snapshot = options.Value ?? throw new InvalidOperationException("PiiEncryption settings are missing.");

        var ring = snapshot.ResolveKeyRing();
        foreach (var k in ring.Keys.Where(k =>
                     string.Equals(k, snapshot.ActiveKeyId.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            _encryptKeyId = k;
            _encryptKeyRaw = ring[k];
            _keys = ring;
            return;
        }

        throw new InvalidOperationException(
            $"PiiEncryption ActiveKeyId '{snapshot.ActiveKeyId}' is not present after validation.");
    }

    public bool IsEnvelope(string? storedValue) =>
        !string.IsNullOrEmpty(storedValue)
        && storedValue.StartsWith(EnvelopePrefix, StringComparison.Ordinal);

    public string? Encrypt(string? plaintext)
    {
        if (string.IsNullOrEmpty(plaintext))
        {
            return null;
        }

        ReadOnlySpan<char> trimmed = plaintext.AsSpan().Trim();
        if (trimmed.IsEmpty)
        {
            return null;
        }

        var plainBytesCount = Encoding.UTF8.GetByteCount(trimmed);
        Span<byte> plainBytes = plainBytesCount <= 4096
            ? stackalloc byte[plainBytesCount]
            : new byte[plainBytesCount];

        Encoding.UTF8.GetBytes(trimmed, plainBytes);

        Span<byte> nonce = stackalloc byte[NonceSizeBytes];
        RandomNumberGenerator.Fill(nonce);

        Span<byte> ciphertext = plainBytes.Length <= 8192 ? stackalloc byte[plainBytes.Length] : new byte[plainBytes.Length];
        Span<byte> tag = stackalloc byte[TagSizeBytes];

        using (var aes = new AesGcm(_encryptKeyRaw, TagSizeBytes))
        {
            aes.Encrypt(nonce, plainBytes, ciphertext, tag);
        }

        var keyIdUtf8 = Encoding.UTF8.GetBytes(_encryptKeyId);
        if (keyIdUtf8.Length > byte.MaxValue)
        {
            throw new InvalidOperationException("PII encryption key id is too long (max 255 UTF-8 bytes).");
        }

        var envelopeLength =
            sizeof(byte)
            + sizeof(byte)
            + keyIdUtf8.Length
            + nonce.Length
            + ciphertext.Length
            + tag.Length;

        Span<byte> envelope = envelopeLength <= 16384
            ? stackalloc byte[envelopeLength]
            : new byte[envelopeLength];

        var offset = 0;
        envelope[offset++] = EnvelopeVersion;
        envelope[offset++] = (byte)keyIdUtf8.Length;
        keyIdUtf8.AsSpan().CopyTo(envelope[offset..]);
        offset += keyIdUtf8.Length;
        nonce.CopyTo(envelope[offset..]);
        offset += nonce.Length;
        ciphertext.CopyTo(envelope[offset..]);
        offset += ciphertext.Length;
        tag.CopyTo(envelope[offset..]);

        return EnvelopePrefix + Convert.ToBase64String(envelope);
    }

    /// <inheritdoc />
    public string Decrypt(string storedValue)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storedValue, nameof(storedValue));
        return DecodeAndDecryptWrapped(storedValue);
    }

    public string? DecryptOrPassThroughLegacy(string? storedValue)
    {
        if (string.IsNullOrEmpty(storedValue))
        {
            return storedValue;
        }

        return IsEnvelope(storedValue)
            ? Decrypt(storedValue)
            : storedValue.Trim();
    }

    public string ReSealWithActiveEncryptor(string envelopeCiphertext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(envelopeCiphertext);
        var plaintext = DecodeAndDecryptWrapped(envelopeCiphertext);
        return EncryptNonEmptyPlaintext(plaintext);
    }

    private string EncryptNonEmptyPlaintext(string plaintext)
    {
        var sealedValue = Encrypt(plaintext);
        if (sealedValue == null)
        {
            throw new PiiDecryptException(
                "PII envelope round-trip decryption produced whitespace-only payloads (unexpected).");
        }

        return sealedValue;
    }

    private static PiiDecryptException WrapDecrypt(Exception inner) =>
        new(
            "PII ciphertext decryption failed — data may have been corrupted or altered while at rest.", inner);

    private string DecodeAndDecryptWrapped(string storedValue)
    {
        try
        {
            return DecodeAndDecrypt(storedValue);
        }
        catch (CryptographicException ex)
        {
            throw WrapDecrypt(ex);
        }
        catch (FormatException ex)
        {
            throw WrapDecrypt(ex);
        }
        catch (ArgumentException ex)
        {
            throw WrapDecrypt(ex);
        }
        catch (PiiDecryptException)
        {
            throw;
        }
    }

    private string DecodeAndDecrypt(string storedValue)
    {
        if (!IsEnvelope(storedValue))
        {
            throw new PiiDecryptException(
                $"PII decrypt expected envelope prefix '{EnvelopePrefix}'. Decrypt(...) does not decode legacy plaintext.");
        }

        var base64Chars = storedValue.AsSpan()[EnvelopePrefix.Length..];
        if (base64Chars.IsEmpty)
        {
            throw new PiiDecryptException("PII ciphertext is empty after prefix.");
        }

        byte[] envelopeBytes;
        try
        {
            envelopeBytes = Convert.FromBase64String(base64Chars.ToString());
        }
        catch (FormatException ex)
        {
            throw new PiiDecryptException("PII ciphertext base64 decoding failed.", ex);
        }

        if (envelopeBytes.Length < 2 + 1 + NonceSizeBytes + TagSizeBytes)
        {
            throw new PiiDecryptException("PII ciphertext truncated.");
        }

        var offset = 0;
        if (envelopeBytes[offset++] != EnvelopeVersion)
        {
            throw new PiiDecryptException("Unsupported or unknown PII encryption envelope version.");
        }

        var keyIdLength = envelopeBytes[offset++];
        if (keyIdLength == 0
            || offset + keyIdLength + NonceSizeBytes + TagSizeBytes > envelopeBytes.Length)
        {
            throw new PiiDecryptException("PII ciphertext malformed (key identifier length invalid).");
        }

        var keyId = Encoding.UTF8.GetString(envelopeBytes, offset, keyIdLength);
        offset += keyIdLength;

        var nonceSlice = envelopeBytes.AsSpan(offset, NonceSizeBytes);
        offset += NonceSizeBytes;

        var authTagCipherSpan = envelopeBytes.AsSpan(offset);
        if (authTagCipherSpan.Length <= TagSizeBytes)
        {
            throw new PiiDecryptException("PII ciphertext malformed (truncated ciphertext).");
        }

        var cipherSpan = authTagCipherSpan[..^TagSizeBytes];
        var tagSpan = authTagCipherSpan[^TagSizeBytes..];

        if (!_keys.TryGetValue(keyId, out var keyRaw))
        {
            throw new PiiDecryptException($"No configured PII key material matches stored key id '{keyId}'.");
        }

        Span<byte> plaintext = cipherSpan.Length <= 8192
            ? stackalloc byte[cipherSpan.Length]
            : new byte[cipherSpan.Length];

        using (var aes = new AesGcm(keyRaw, TagSizeBytes))
        {
            aes.Decrypt(nonceSlice, cipherSpan, tagSpan, plaintext);
        }

        return Encoding.UTF8.GetString(plaintext);
    }
}
