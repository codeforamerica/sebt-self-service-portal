namespace SEBT.Portal.Core.Services;

/// <summary>
/// Hashes SSN for secure storage. Uses HMAC-SHA256 so the same plaintext produces the same hash for lookup.
/// </summary>
public interface IIdentifierHasher
{
    /// <summary>
    /// Hashes a plaintext SSN for storage. Normalizes by stripping dashes and spaces before hashing.
    /// </summary>
    /// <param name="plaintext">The plaintext SSN to hash.</param>
    /// <returns>The HMAC-SHA256 hash as a 64-character hex string, or null if input is null/whitespace.</returns>
    string? Hash(string? plaintext);

    /// <summary>
    /// Verifies that the given plaintext SSN produces the stored hash.
    /// </summary>
    /// <param name="plaintext">The plaintext SSN to verify.</param>
    /// <param name="storedHash">The hash stored in the database.</param>
    /// <returns>True if the plaintext hashes to the stored hash.</returns>
    bool Matches(string? plaintext, string? storedHash);

    /// <summary>
    /// Returns the value suitable for storage. If the value is already a stored hash (64 hex chars), returns as-is.
    /// Otherwise hashes the plaintext. Use when updating users to avoid double-hashing.
    /// </summary>
    string? HashForStorage(string? value);
}
