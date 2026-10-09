using System.Text.Json.Nodes;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Write-side path writer: sets a value at a dotted target path, building intermediate objects as
/// needed. Dotted property paths only — no <c>$</c>/<c>[index]</c> grammar, narrower than the
/// read-side <see cref="JsonPathSelector"/> by design. Empty segments and the read-side grammar
/// fail loud so a bad <c>map</c> target is rejected at load.
/// </summary>
internal static class JsonPathWriter
{
    public static void Write(JsonObject root, string dottedPath, JsonNode? value)
    {
        Validate(dottedPath);

        string[] segments = dottedPath.Split('.');
        JsonObject current = root;

        for (int i = 0; i < segments.Length - 1; i++)
        {
            string segment = segments[i];
            if (current[segment] is not JsonObject child)
            {
                child = new JsonObject();
                current[segment] = child;
            }

            current = child;
        }

        current[segments[^1]] = value;
    }

    /// <summary>
    /// Fails loud when <paramref name="dottedPath"/> is empty, contains an empty segment, or uses
    /// <c>$</c>/<c>[index]</c>
    /// </summary>
    public static void Validate(string dottedPath)
    {
        ArgumentNullException.ThrowIfNull(dottedPath);

        if (dottedPath.Length == 0
            || dottedPath.Contains('$')
            || dottedPath.Contains('[')
            || dottedPath.Contains(']'))
        {
            throw Malformed(dottedPath);
        }

        foreach (string segment in dottedPath.Split('.'))
        {
            if (segment.Length == 0)
            {
                throw Malformed(dottedPath);
            }
        }
    }

    private static InvalidOperationException Malformed(string dottedPath) =>
        new($"Malformed path '{dottedPath}'.");
}
