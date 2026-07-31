using System.Buffers.Text;
using System.Text.Json;

namespace SEBT.Portal.Core.StateBackends;

/// <summary>
/// Packs a case's named routing fields into a URL-safe base64 JSON token; reads compose it, writes
/// decode it. The JSON-adapter driver composes from config (see
/// <see cref="Configuration.Operations.CaseIdComposition"/>); the plugin read path composes a fixed
/// field set. The pack/unpack mechanism is fixed here so both integration paths share one token
/// namespace.
/// </summary>
/// <remarks>
/// The token is opaque, not encrypted — it carries no secrets, only routing identifiers the backend
/// already returned. A mangled token fails to decode; it is not cryptographically signed.
/// Serialization preserves the dictionary's insertion order, so callers that build fields in a fixed
/// order get byte-identical tokens for the same record — callers rely on that determinism.
/// </remarks>
public static class OpaqueCaseId
{
    /// <summary>Packs the named routing fields into a URL-safe base64 JSON token.</summary>
    public static string Compose(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(fields);
        return Base64Url.EncodeToString(json);
    }

    /// <summary>Decodes a token back into its named routing fields. Fails loud on a malformed token.</summary>
    /// <remarks>
    /// Failure messages never echo the token: it can carry email/phone in
    /// <c>householdIdentifier</c>, and callers log these exceptions in full.
    /// </remarks>
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
            throw new InvalidOperationException("caseId token is not valid base64.", ex);
        }

        Dictionary<string, string>? fields;
        try
        {
            fields = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("caseId token does not decode to routing fields.", ex);
        }

        return fields
            ?? throw new InvalidOperationException("caseId token decoded to no routing fields.");
    }
}
