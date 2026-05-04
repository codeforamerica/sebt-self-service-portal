using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// AES-GCM key material configuration for reversible PII column encryption (see ADR).
/// </summary>
public class PiiEncryptionSettings
{
    public const string SectionName = "PiiEncryption";

    /// <summary>The key id entries are encrypted or re-encrypted with at write time.</summary>
    [Required(ErrorMessage = "PiiEncryption:ActiveKeyId is required.")]
    [MinLength(1)]
    public string ActiveKeyId { get; set; } = "";

    /// <summary>Historical + active symmetric keys keyed by logical id.</summary>
    [Required(ErrorMessage = "PiiEncryption:Keys is required.")]
    [MinLength(1, ErrorMessage = "PiiEncryption requires at least one key entry.")]
    public List<PiiEncryptionKeySetting> Keys { get; set; } = [];

    public IReadOnlyDictionary<string, byte[]> ResolveKeyRing()
    {
        var dictionary = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var k in Keys)
        {
            var id = k.KeyId.Trim();
            if (dictionary.ContainsKey(id))
            {
                throw new InvalidOperationException($"Duplicate PII encryption KeyId '{id}'.");
            }

            dictionary[id] = Convert.FromBase64String(k.KeyMaterialBase64.Trim());
        }

        foreach (var (id, raw) in dictionary)
        {
            if (raw.Length is not (16 or 24 or 32))
            {
                throw new InvalidOperationException(
                    $"PII encryption key '{id}' length must be 16, 24, or 32 bytes.");
            }
        }

        var active = ActiveKeyId.Trim();
        if (active.Length == 0 ||
            !dictionary.Keys.Any(k => string.Equals(k, active, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"PiiEncryption:ActiveKeyId '{ActiveKeyId}' was not found in PiiEncryption:Keys.");
        }

        return dictionary;
    }
}

public class PiiEncryptionKeySetting
{
    /// <summary>
    /// Logical identifier embedded in ciphertext (stable across deployments for rotation/decrypt).
    /// </summary>
    [Required(ErrorMessage = "Each PII encryption key entry requires KeyId.")]
    [MinLength(1)]
    public string KeyId { get; set; } = "";

    /// <summary>Raw AES key bytes (UTF-16 — store base64 in configuration).</summary>
    [Required(ErrorMessage = "Each PII encryption key entry requires KeyMaterialBase64.")]
    [MinLength(1)]
    public string KeyMaterialBase64 { get; set; } = "";
}
