using System.Text.Json;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Reads a named property off a JSON object as a string, fail-soft — a miss is always <c>null</c>,
/// never an exception. Numbers keep their raw text (<c>"00"</c> keeps its leading zero) and bools
/// read as <c>"true"</c>/<c>"false"</c> — load-bearing for valueIn and messageContains matching.
/// </summary>
internal static class JsonRead
{
    public static string? AsString(JsonElement parent, string property)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(property, out JsonElement value))
        {
            return null;
        }

        return AsString(value);
    }

    /// <summary>Same coercion off a nullable parent (null when the backend returned no/invalid JSON).</summary>
    public static string? AsString(JsonElement? parent, string property) =>
        parent is { } root ? AsString(root, property) : null;

    /// <summary>Coerces an already-selected element (e.g. from a path selector) as a string.</summary>
    public static string? AsString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
}
