using System.Globalization;
using System.Text.Json;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Response-side fan-in for the enrollment op (DC-568 spike). Selects the response rows at
/// <see cref="EnrollmentResponseMapping.Root"/>, classifies each row as a match via the single
/// closed <see cref="EnrollmentResponseMapping.MatchWhen"/> condition (a body field's value in a
/// set — the eligibility flag), and fans those verdicts back in by the echoed correlation index:
/// a child (1-based request index) matches when ANY of its candidate rows matched.
///
/// HARD CAP: the match predicate is the write-classifier's <c>valueIn(field)</c> kind ONLY — no
/// numeric thresholds, no confidence scoring, no fuzzy matching. A row whose index doesn't map to a
/// requested child is ignored (fan-in is per requested child, in request order).
/// </summary>
internal static class EnrollmentResponseCorrelator
{
    public static EnrollmentCheckResult Correlate(
        EnrollmentResponseMapping mapping, JsonElement root, EnrollmentCheckRequest request)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(request);

        // Batch mode: the validator guarantees a non-null index field before we get here.
        string indexField = mapping.IndexField
            ?? throw new InvalidOperationException("Batch enrollment response mapping requires an indexField.");

        // Collect the set of correlation indices whose rows matched.
        var matchedIndices = new HashSet<string>(StringComparer.Ordinal);
        JsonElement rows = SelectPath(root, mapping.Root);

        if (rows.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement row in rows.EnumerateArray())
            {
                string? index = ReadString(row, indexField);
                if (index is not null && RowMatches(mapping.MatchWhen, row))
                {
                    matchedIndices.Add(index);
                }
            }
        }

        var results = new List<EnrollmentChildResult>(request.Children.Count);
        for (int i = 0; i < request.Children.Count; i++)
        {
            string index = (i + 1).ToString(CultureInfo.InvariantCulture);
            results.Add(new EnrollmentChildResult(request.Children[i].CheckId, matchedIndices.Contains(index)));
        }

        return new EnrollmentCheckResult(results);
    }

    /// <summary>
    /// PerChild evaluation: selects the single result object at <see cref="EnrollmentResponseMapping.Root"/>
    /// and applies the closed <see cref="EnrollmentResponseMapping.MatchWhen"/> predicate to it. No
    /// correlation index — one call reads one child's verdict.
    /// </summary>
    public static bool EvaluateSingleResult(EnrollmentResponseMapping mapping, JsonElement root)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        JsonElement result = SelectPath(root, mapping.Root);
        return RowMatches(mapping.MatchWhen, result);
    }

    private static bool RowMatches(EnrollmentMatchCondition condition, JsonElement row)
    {
        string? value = ReadString(row, condition.Field);
        return value is not null && condition.ValueIn.Contains(value, StringComparer.Ordinal);
    }

    private static string? ReadString(JsonElement record, string property)
    {
        if (record.ValueKind != JsonValueKind.Object
            || !record.TryGetProperty(property, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    // Capped path grammar: leading `$`, dotted property segments, `[index]` element access.
    // Mirrors the response mapper's selector — not a general JSONPath engine.
    private static JsonElement SelectPath(JsonElement root, string path)
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
