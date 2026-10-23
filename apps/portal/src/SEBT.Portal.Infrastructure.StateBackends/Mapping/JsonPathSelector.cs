using System.Globalization;
using System.Text.Json;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Read-side path selector: a leading <c>$</c>, dotted property segments, and <c>[index]</c>
/// element access only — not a general JSONPath engine. Returns a
/// <see cref="JsonValueKind.Undefined"/> element when the path does not resolve.
/// </summary>
internal static class JsonPathSelector
{
    public static JsonElement Select(JsonElement root, string path)
    {
        Validate(path);

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

            while (bracket >= 0)
            {
                int close = segment.IndexOf(']', bracket);
                int index = int.Parse(segment[(bracket + 1)..close], NumberStyles.None, CultureInfo.InvariantCulture);
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

    public static void Validate(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        string trimmed = TrimRoot(path);
        if (trimmed.Length == 0)
        {
            return;
        }

        foreach (string segment in trimmed.Split('.'))
        {
            ValidateSegment(segment, path);
        }
    }

    private static void ValidateSegment(string segment, string path)
    {
        if (segment.Length == 0)
        {
            throw Malformed(path);
        }

        int bracket = segment.IndexOf('[');
        if (bracket < 0)
        {
            if (segment.Contains(']'))
            {
                throw Malformed(path);
            }

            return;
        }

        while (bracket >= 0)
        {
            int close = segment.IndexOf(']', bracket);
            if (close < 0)
            {
                throw Malformed(path);
            }

            string indexText = segment[(bracket + 1)..close];
            if (!int.TryParse(indexText, NumberStyles.None, CultureInfo.InvariantCulture, out int index)
                || index < 0)
            {
                throw Malformed(path);
            }

            int next = segment.IndexOf('[', close);
            if (next >= 0 && next != close + 1)
            {
                throw Malformed(path);
            }

            if (next < 0 && close != segment.Length - 1)
            {
                throw Malformed(path);
            }

            bracket = next;
        }
    }

    private static IEnumerable<string> SplitPath(string path)
    {
        string trimmed = TrimRoot(path);

        return trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries);
    }

    private static string TrimRoot(string path)
    {
        if (path.StartsWith("$.", StringComparison.Ordinal))
        {
            return path[2..];
        }

        return path.StartsWith('$') ? path[1..] : path;
    }

    private static InvalidOperationException Malformed(string path) =>
        new($"Malformed path '{path}'.");
}
