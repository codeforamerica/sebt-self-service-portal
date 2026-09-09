using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// HMAC-SHA256 implementation of <see cref="IHMACSHA256Hasher"/> backed by
/// <see cref="IdentifierHasherSettings.SecretKey"/>.
/// Produces a lowercase hex-encoded 64-character hash of the input string.
/// This is the underlying primitive used by state connectors that need a
/// deterministic, keyed hash without the normalization layer of
/// <see cref="IdentifierHasher"/>.
/// </summary>
public class HMACSHA256Hasher : IHMACSHA256Hasher
{
    private readonly byte[] _keyBytes;

    public HMACSHA256Hasher(IOptions<IdentifierHasherSettings> options)
    {
        var secretKey = options?.Value?.SecretKey
            ?? throw new InvalidOperationException("IdentifierHasher:SecretKey must be configured.");
        _keyBytes = Encoding.UTF8.GetBytes(secretKey);
        if (_keyBytes.Length < 32)
        {
            throw new InvalidOperationException("IdentifierHasher:SecretKey must be at least 32 characters.");
        }
    }

    /// <inheritdoc />
    public string Hash(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var hashBytes = HMACSHA256.HashData(_keyBytes, Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
