namespace SEBT.Portal.StatesPlugins.Interfaces.Services;

/// <summary>
/// Domain-agnostic HMAC-SHA256 hasher. Implementations use a configured secret key
/// to produce deterministic hashes suitable for cache keys, lookup tokens, and
/// other consistent-hash use cases. Input is hashed as UTF-8 bytes; the output is
/// a lowercase hexadecimal string of the 32-byte HMAC.
/// </summary>
public interface IHMACSHA256Hasher
{
    /// <summary>
    /// Computes HMAC-SHA256 over the UTF-8 bytes of <paramref name="input"/> using the
    /// implementation's configured secret key. Returns the result as a 64-character
    /// lowercase hexadecimal string. Throws if <paramref name="input"/> is null.
    /// </summary>
    string Hash(string input);
}
