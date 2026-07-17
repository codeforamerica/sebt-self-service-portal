using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration for the identifier hasher.
/// The secret key should be stored in configuration and/or secrets manager.
/// </summary>
public class IdentifierHasherSettings
{
    public static readonly string SectionName = "IdentifierHasher";

    /// <summary>
    /// The secret key for HMAC-SHA256 hashing of stored identifiers
    /// (cooldown lookups, deduplication). Must be at least 32 bytes.
    /// Rotating this key invalidates every existing stored hash, so keep
    /// it long-lived and separate from the analytics key.
    /// </summary>
    [Required(ErrorMessage = "IdentifierHasher:SecretKey is required for secure identifier storage.")]
    [MinLength(32, ErrorMessage = "IdentifierHasher:SecretKey must be at least 32 characters.")]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Optional separate secret used by HashForAnalytics. Kept distinct from
    /// SecretKey so the analytics key can be rotated freely without
    /// invalidating stored cooldown hashes. When unset, HashForAnalytics
    /// falls back to SecretKey for backward compatibility.
    /// </summary>
    [MinLength(32, ErrorMessage = "IdentifierHasher:AnalyticsSecretKey must be at least 32 characters.")]
    public string? AnalyticsSecretKey { get; set; }
}
