using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Hashes sensitive identifiers (like Ssn, SNAP ID etc.) for secure storage.
/// Uses HMAC-SHA256 so the same plaintext produces the same hash for lookup.
/// </summary>
public interface IIdentifierHasher
{
    /// <summary>
    /// Hashes a plaintext identifier for storage. Uses type-specific normalization before hashing.
    /// </summary>
    /// <param name="type">The identifier type.</param>
    /// <param name="plaintext">The plaintext value to hash.</param>
    /// <returns>The HMAC-SHA256 hash as a 64-character hex string, or null if input is null/whitespace.</returns>
    string? Hash(PreferredHouseholdIdType type, string? plaintext);

    /// <summary>
    /// Verifies that the given plaintext produces the stored hash.
    /// </summary>
    /// <param name="type">The identifier type.</param>
    /// <param name="plaintext">The plaintext to verify.</param>
    /// <param name="storedHash">The hash stored in the database.</param>
    /// <returns>True if the plaintext hashes to the stored hash.</returns>
    bool Matches(PreferredHouseholdIdType type, string? plaintext, string? storedHash);

    /// <summary>
    /// Returns the value suitable for storage. If the value is already a stored hash (64 hex chars), returns as-is.
    /// Otherwise hashes the plaintext. Use when updating users to avoid double-hashing.
    /// </summary>
    string? HashForStorage(PreferredHouseholdIdType type, string? value);
}
