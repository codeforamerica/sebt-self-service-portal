using System.Text.Json.Nodes;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Shared write-side path writer: sets a value at a dotted target path, building intermediate
/// nested objects as needed. Dotted property paths ONLY — deliberately NO <c>$</c>/<c>[index]</c>
/// grammar. This is a separate primitive from the read-side <c>JsonPathSelector</c> by design;
/// keep each brick narrow.
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
