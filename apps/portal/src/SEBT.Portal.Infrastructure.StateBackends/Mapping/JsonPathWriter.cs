using System.Text.Json.Nodes;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Write-side path writer: sets a value at a dotted target path, building intermediate objects as
/// needed. Dotted property paths only — no <c>$</c>/<c>[index]</c> grammar, narrower than the
/// read-side <see cref="JsonPathSelector"/> by design.
/// </summary>
internal static class JsonPathWriter
{
    public static void Write(JsonObject root, string dottedPath, JsonNode? value)
    {
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
}
