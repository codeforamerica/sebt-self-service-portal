using System.Text.Json;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Shared read-side helper: reads a named property off a JSON object as a string, coercing the
/// scalar kinds the DC-568 mapping bricks need. Consolidates three copies that had DIVERGED — the
/// mapper (string-only), the write-result classifier (string + number-as-raw-text), and the
/// enrollment correlator (string + number + bool). Unified here on the SUPERSET.
///
/// Coercion contract (fail-soft — a miss is always <c>null</c>, never an exception):
///   * String  → the string value.
///   * Number  → its raw JSON text (e.g. <c>respCd: "00"</c> must round-trip as <c>"00"</c>, not
///     lose its leading zero; a raw read keeps the source token exactly).
///   * True/False → <c>"true"</c> / <c>"false"</c> (so a boolean <c>isEligible: true</c> can be
///     matched by a <c>valueIn: ["true"]</c> rule).
///   * Null / Undefined / a non-object parent / an absent property → <c>null</c>.
///
/// The number/bool stringification is load-bearing for value-in-set and message-contains matching;
/// the mapper's typed-coercion layer (dates/enums via FieldMapping) re-parses from this raw read,
/// so widening the mapper's read to also stringify numbers/bools is inert for string-typed fields
/// and turns a would-be <see cref="JsonElement.GetString"/> throw on a numeric/bool field into a
/// graceful value.
/// </summary>
internal static class JsonRead
{
    /// <summary>
    /// Reads <paramref name="property"/> off <paramref name="parent"/> as a coerced string, or
    /// <c>null</c> when the parent is not an object, the property is absent, or the value is
    /// null/undefined.
    /// </summary>
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
    /// Reads <paramref name="property"/> off a nullable parent (the parsed body root, or
    /// <c>null</c> when the backend returned no/invalid JSON), applying the same coercion.
    /// </summary>
    public static string? AsString(JsonElement? parent, string property) =>
        parent is { } root ? AsString(root, property) : null;

    private static string? AsString(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
}
