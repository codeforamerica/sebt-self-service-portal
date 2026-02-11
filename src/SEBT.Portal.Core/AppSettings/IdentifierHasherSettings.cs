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
    /// The secret key for HMAC-SHA256 hashing. Must be at least 32 bytes.
    /// </summary>
    [Required(ErrorMessage = "IdentifierHasher:SecretKey is required for secure identifier storage.")]
    [MinLength(32, ErrorMessage = "IdentifierHasher:SecretKey must be at least 32 characters.")]
    public string SecretKey { get; set; } = string.Empty;
}
