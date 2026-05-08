using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// HMAC-SHA256 implementation of <see cref="IIdentifierHasher"/> for hashing sensitive identifiers.
/// </summary>
public class IdentifierHasher : IIdentifierHasher
{
    private readonly byte[] _keyBytes;
    private readonly byte[] _analyticsKeyBytes;
    private const int HashLengthHex = 64;

    public IdentifierHasher(IOptions<IdentifierHasherSettings> options)
    {
        var settings = options?.Value
            ?? throw new InvalidOperationException("IdentifierHasher settings must be configured.");

        var secretKey = settings.SecretKey
            ?? throw new InvalidOperationException("IdentifierHasher:SecretKey must be configured.");
        _keyBytes = Encoding.UTF8.GetBytes(secretKey);
        if (_keyBytes.Length < 32)
        {
            throw new InvalidOperationException("IdentifierHasher:SecretKey must be at least 32 characters.");
        }

        // Separate analytics key keeps rotation safe: rotating the analytics
        // secret only churns vendor-side digests, leaving stored cooldown
        // hashes intact. Falls back to SecretKey when unset for back-compat.
        var analyticsKey = settings.AnalyticsSecretKey;
        _analyticsKeyBytes = !string.IsNullOrEmpty(analyticsKey)
            ? Encoding.UTF8.GetBytes(analyticsKey)
            : _keyBytes;
    }

    /// <inheritdoc />
    public string? Hash(string? plaintext)
    {
        var normalized = IdentifierNormalizer.NormalizeOrNull(plaintext);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var hash = HMACSHA256.HashData(_keyBytes, Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }

    /// <inheritdoc />
    public bool Matches(string? plaintext, string? storedHash)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || storedHash.Length != HashLengthHex)
        {
            return false;
        }

        var computed = Hash(plaintext);
        if (computed == null)
        {
            return false;
        }

        var computedBytes = Convert.FromHexString(computed);
        var storedBytes = Convert.FromHexString(storedHash);
        return CryptographicOperations.FixedTimeEquals(computedBytes, storedBytes);
    }

    /// <inheritdoc />
    public string? HashForAnalytics(string? plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext))
        {
            return null;
        }

        var hash = HMACSHA256.HashData(_analyticsKeyBytes, Encoding.UTF8.GetBytes(plaintext));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <inheritdoc />
    public string? HashForStorage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // If already a stored hash (64 hex chars), pass through to avoid double-hashing on updates
        if (value.Length == HashLengthHex && value.All(IsHexChar))
        {
            return value;
        }

        return Hash(value);
    }

    private static bool IsHexChar(char c) =>
        c is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F');
}
