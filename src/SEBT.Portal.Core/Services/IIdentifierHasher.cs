namespace SEBT.Portal.Core.Services;

/// <summary>
/// Hashes sensitive identifiers (including low-entropy values like SSNs) for secure storage and search.
/// Uses HMAC-SHA256 so the same plaintext produces the same hash for lookup.
/// </summary>
public interface IIdentifierHasher
{
    /// <summary>
    /// Hashes a plaintext identifier for storage. Input normalization is implementation-specific.
    /// </summary>
    /// <param name="plaintext">The plaintext identifier to hash.</param>
    /// <returns>The HMAC-SHA256 hash as a 64-character hex string, or null if input is null/whitespace.</returns>
    string? Hash(string? plaintext);

    /// <summary>
    /// Verifies that the given plaintext produces the stored hash using constant-time comparison.
    /// </summary>
    /// <param name="plaintext">The plaintext identifier to verify.</param>
    /// <param name="storedHash">The hash stored in the database.</param>
    /// <returns>True if the plaintext hashes to the stored hash.</returns>
    bool Matches(string? plaintext, string? storedHash);

    /// <summary>
    /// Returns the value suitable for storage. If the value is already a stored hash (64 hex chars), returns as-is.
    /// Otherwise hashes the plaintext. Use when updating records to avoid double-hashing.
    /// </summary>
    string? HashForStorage(string? value);

    /// <summary>
    /// Hashes a value for emission to analytics tools. Skips the storage-side
    /// normalization (no trim, no dash/space stripping) and returns lowercase
    /// hex so external pipelines can reproduce the digest from a published
    /// reference algorithm. Returns null if input is null/whitespace.
    /// See docs/analytics/hashed-sebt-app-id.md for the contract.
    /// </summary>
    /// <param name="plaintext">The plaintext identifier to hash.</param>
    /// <returns>The HMAC-SHA256 hash as a 64-character lowercase hex string, or null.</returns>
    string? HashForAnalytics(string? plaintext);
}
