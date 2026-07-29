using System.Globalization;
using System.Text.Json;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Read-side path selector shared by the response mapper and enrollment correlator.
/// Navigates a capped path: a leading <c>$</c>, dotted property segments, and <c>[index]</c>
/// element access. Anything else is rejected — this is not a general JSONPath engine.
/// Returns <c>default</c> (a <see cref="JsonValueKind.Undefined"/> element) when the path
/// does not resolve; callers distinguish a miss via <see cref="JsonElement.ValueKind"/>.
/// </summary>
internal static class JsonPathSelector
{
    public static JsonElement Select(JsonElement root, string path)
    {
        JsonElement current = root;

        foreach (string segment in SplitPath(path))
        {
            int bracket = segment.IndexOf('[');
            string property = bracket >= 0 ? segment[..bracket] : segment;

            if (property.Length > 0)
            {
                if (current.ValueKind != JsonValueKind.Object
                    || !current.TryGetProperty(property, out current))
                {
                    return default;
                }
            }

            // Handle a trailing [index] on this segment, e.g. resultSets[0].
            while (bracket >= 0)
            {
                int close = segment.IndexOf(']', bracket);
                if (close < 0)
                {
                    throw new FormatException($"Malformed path segment '{segment}' in '{path}'.");
                }

                int index = int.Parse(segment[(bracket + 1)..close], CultureInfo.InvariantCulture);
                if (current.ValueKind != JsonValueKind.Array || index >= current.GetArrayLength())
                {
                    return default;
                }

                current = current[index];
                bracket = segment.IndexOf('[', close);
            }
        }

        return current;
    }

    private static IEnumerable<string> SplitPath(string path)
    {
        string trimmed = path.StartsWith("$.", StringComparison.Ordinal)
            ? path[2..]
            : path.StartsWith('$') ? path[1..] : path;

        return trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
    }
}
