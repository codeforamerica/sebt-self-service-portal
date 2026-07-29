using System.Buffers.Text;
using System.Text.Json;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Packs a write's named routing fields into a URL-safe base64 JSON token; reads compose it, writes
/// decode it. Config declares which fields go in (see
/// <see cref="Core.StateBackends.Configuration.Operations.CaseIdComposition"/>); the pack/unpack
/// mechanism is fixed here.
/// </summary>
/// <remarks>
/// The token is opaque, not encrypted — it carries no secrets, only routing identifiers the backend
/// already returned. A mangled token fails to decode; it is not cryptographically signed.
/// </remarks>
internal static class OpaqueCaseId
{
    /// <summary>Packs the named routing fields into a URL-safe base64 JSON token.</summary>
    public static string Compose(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(fields);
        return Base64Url.EncodeToString(json);
    }

    /// <summary>Decodes a token back into its named routing fields. Fails loud on a malformed token.</summary>
    public static IReadOnlyDictionary<string, string> Decode(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        byte[] json;
        try
        {
            json = Base64Url.DecodeFromChars(token);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"caseId token '{token}' is not valid base64.", ex);
        }

        Dictionary<string, string>? fields;
        try
        {
            fields = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"caseId token '{token}' does not decode to routing fields.", ex);
        }

        return fields
            ?? throw new InvalidOperationException($"caseId token '{token}' decoded to no routing fields.");
    }
}
