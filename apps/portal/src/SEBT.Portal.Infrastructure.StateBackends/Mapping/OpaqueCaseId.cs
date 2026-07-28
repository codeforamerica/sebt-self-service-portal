using System.Text.Json;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// The FIXED platform primitive for the opaque, self-describing caseId token (DC-568 spike).
/// A caseId packs a small set of named routing fields — the fields a write needs to route the
/// backend call — into a JSON object, then URL-safe base64-encodes it. Reads compose it; writes
/// decode it. This is NOT config-driven: config only declares WHICH fields go in (see
/// <see cref="Core.StateBackends.Configuration.Operations.CaseIdComposition"/>); the pack/unpack
/// mechanism lives entirely here.
/// </summary>
/// <remarks>
/// The token is opaque, not encrypted — it carries no secrets, only the routing identifiers the
/// backend already returned on the read. It is tamper-evident only insofar as a mangled token
/// fails to decode (fail-loud), not cryptographically signed.
/// </remarks>
internal static class OpaqueCaseId
{
    /// <summary>Packs the named routing fields into a URL-safe base64 JSON token.</summary>
    public static string Compose(IReadOnlyDictionary<string, string> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        byte[] json = JsonSerializer.SerializeToUtf8Bytes(fields);
        return ToUrlSafeBase64(json);
    }

    /// <summary>Decodes a token back into its named routing fields. Fails loud on a malformed token.</summary>
    public static IReadOnlyDictionary<string, string> Decode(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        byte[] json;
        try
        {
            json = FromUrlSafeBase64(token);
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

    // URL-safe base64: '+' -> '-', '/' -> '_', padding stripped (mirrors RFC 4648 §5).
    private static string ToUrlSafeBase64(byte[] bytes)
    {
        string standard = Convert.ToBase64String(bytes);
        return standard.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static byte[] FromUrlSafeBase64(string token)
    {
        string standard = token.Replace('-', '+').Replace('_', '/');
        int padding = standard.Length % 4;
        if (padding > 0)
        {
            standard = standard.PadRight(standard.Length + (4 - padding), '=');
        }

        return Convert.FromBase64String(standard);
    }
}
