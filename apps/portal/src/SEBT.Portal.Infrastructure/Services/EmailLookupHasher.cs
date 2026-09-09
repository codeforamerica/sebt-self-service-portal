using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>HMAC-SHA256 over normalized-email bytes for deterministic SQL equality lookups.</summary>
public sealed class EmailLookupHasher : IEmailLookupHasher
{
    private const string DomainSeparatorUtf8 = "sep|portal|email:v1|";

    private readonly byte[] _keyBytes;

    public EmailLookupHasher(IOptions<IdentifierHasherSettings> options)
    {
        var secretKey = options?.Value?.SecretKey
            ?? throw new InvalidOperationException("IdentifierHasher:SecretKey must be configured.");
        _keyBytes = Encoding.UTF8.GetBytes(secretKey);
        if (_keyBytes.Length < 32)
        {
            throw new InvalidOperationException("IdentifierHasher:SecretKey must be at least 32 bytes.");
        }
    }

    /// <inheritdoc />
    public string? NormalizeAndHash(string? email)
    {
        var normalized = EmailNormalizer.NormalizeOrNull(email);
        return HashNormalized(normalized);
    }

    /// <inheritdoc />
    public string? HashNormalized(string? normalizedLowercaseTrimmedEmail)
    {
        if (string.IsNullOrWhiteSpace(normalizedLowercaseTrimmedEmail))
        {
            return null;
        }

        var prefixBytes = Encoding.UTF8.GetBytes(DomainSeparatorUtf8);
        var normalizedBytes = Encoding.UTF8.GetBytes(normalizedLowercaseTrimmedEmail);
        var combined = new byte[prefixBytes.Length + normalizedBytes.Length];
        Buffer.BlockCopy(prefixBytes, 0, combined, 0, prefixBytes.Length);
        Buffer.BlockCopy(normalizedBytes, 0, combined, prefixBytes.Length, normalizedBytes.Length);
        var mac = HMACSHA256.HashData(_keyBytes, combined);

        return Convert.ToHexString(mac);
    }
}
