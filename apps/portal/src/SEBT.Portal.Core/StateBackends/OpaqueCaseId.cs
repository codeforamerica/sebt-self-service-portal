using System.Buffers.Text;
using System.Text.Json;

namespace SEBT.Portal.Core.StateBackends;

/// <summary>
/// Packs a case's routing fields into a URL-safe base64 JSON token; reads compose it, writes decode
/// it. Fixed here so both integration paths share one token namespace.
/// </summary>
/// <remarks>
/// Opaque, not encrypted — it carries only routing identifiers the backend already returned.
/// Insertion order is preserved, so a fixed field order yields byte-identical tokens; callers rely on that.
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

    /// <summary>Decodes a token back into its routing fields. Fails loud on a malformed token.</summary>
    /// <remarks>Failure messages never echo the token — it can carry email/phone, and callers log in full.</remarks>
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
