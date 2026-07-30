namespace SEBT.Portal.Core.Services;

/// <summary>
/// Resolves a case ID — however the read path encoded it — to the canonical,
/// path-stable identity string to hash for cooldown lookup/persist.
/// </summary>
/// <remarks>
/// Cooldown rows are keyed by <see cref="IIdentifierHasher"/> hashes of this
/// value. Hashing is one-way, so the hash input must never change with the ID
/// encoding a read path happens to serve — otherwise every household's cooldown
/// silently resets at an integration cutover.
/// </remarks>
public interface ICooldownIdentityResolver
{
    /// <summary>
    /// Returns the canonical, path-stable identity string to hash for cooldown
    /// lookup/persist. Never returns an encoding-specific token.
    /// </summary>
    string ResolveCanonicalCaseIdentity(string caseId);
}
