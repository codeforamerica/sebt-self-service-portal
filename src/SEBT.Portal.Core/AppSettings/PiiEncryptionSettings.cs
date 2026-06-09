namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// AES-256-GCM key material configuration for reversible PII column encryption (see ADR).
/// </summary>
public class PiiEncryptionSettings
{
    public const string SectionName = "PiiEncryption";

    /// <summary>Decoded key material must be exactly this many bytes (256-bit AES only).</summary>
    public const int RequiredKeyMaterialLengthBytes = 32;

    /// <summary>
    /// When true, new and updated PII columns are stored as AES-GCM envelopes. When false, writes persist trimmed plaintext
    /// (reads still decrypt existing envelopes). Opt in per state/deployment; when true in production, keys are validated (see PiiEncryptionGuard).
    /// </summary>
    public bool EncryptAtRest { get; set; } = false;

    /// <summary>
    /// When true, <see cref="Infrastructure.Services.PiiPlaintextEncryptionBackfill"/> runs after EF migrations on startup.
    /// </summary>
    public bool RunStartupBackfill { get; set; } = false;

    /// <summary>The key id entries are encrypted or re-encrypted with at write time. Required when <see cref="EncryptAtRest"/> is true.</summary>
    public string ActiveKeyId { get; set; } = "";

    /// <summary>Historical + active symmetric keys keyed by logical id. Required when <see cref="EncryptAtRest"/> is true.</summary>
    public List<PiiEncryptionKeySetting> Keys { get; set; } = [];

    /// <summary>True when ActiveKeyId and at least one non-empty key entry are bound (coherence validated separately when encryption is on).</summary>
    public bool HasKeyRingConfiguration()
    {
        if (string.IsNullOrWhiteSpace(ActiveKeyId) || Keys == null || Keys.Count == 0)
        {
            return false;
        }

        foreach (var entry in Keys)
        {
            if (entry == null
                || string.IsNullOrWhiteSpace(entry.KeyId)
                || string.IsNullOrWhiteSpace(entry.KeyMaterialBase64))
            {
                return false;
            }
        }

        return true;
    }

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
            if (raw.Length != RequiredKeyMaterialLengthBytes)
            {
                throw new InvalidOperationException(
                    $"PII encryption key '{id}' must decode to exactly {RequiredKeyMaterialLengthBytes} bytes (256-bit AES-GCM); actual length was {raw.Length}.");
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
    public string KeyId { get; set; } = "";

    /// <summary>Raw AES-256 key bytes (store Base64 in configuration; decoded length must be 32).</summary>
    public string KeyMaterialBase64 { get; set; } = "";
}
