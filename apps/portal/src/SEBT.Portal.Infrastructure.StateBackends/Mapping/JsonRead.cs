using System.Text.Json;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Shared read-side helper: reads a named property off a JSON object as a string. Coercion contract
/// (fail-soft — a miss is always <c>null</c>, never an exception):
///   * String → the string value.
///   * Number → its raw JSON text, so <c>respCd: "00"</c> round-trips as <c>"00"</c> without losing
///     the leading zero.
///   * True/False → <c>"true"</c> / <c>"false"</c>, so <c>isEligible: true</c> matches a
///     <c>valueIn: ["true"]</c> rule.
///   * Null / Undefined / non-object parent / absent property → <c>null</c>.
/// The number/bool stringification is load-bearing for value-in-set and message-contains matching.
/// </summary>
internal static class JsonRead
{
    /// <summary>Reads <paramref name="property"/> off <paramref name="parent"/> as a coerced string.</summary>
    public static string? AsString(JsonElement parent, string property)
    {
        if (parent.ValueKind != JsonValueKind.Object
            || !parent.TryGetProperty(property, out JsonElement value))
        {
            return null;
        }

        return AsString(value);
    }

    /// <summary>
    /// Reads <paramref name="property"/> off a nullable parent (null when the backend returned
    /// no/invalid JSON), applying the same coercion.
    /// </summary>
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
