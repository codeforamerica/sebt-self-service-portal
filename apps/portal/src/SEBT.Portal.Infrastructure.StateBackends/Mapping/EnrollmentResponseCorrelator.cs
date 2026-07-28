using System.Globalization;
using System.Text.Json;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Response-side fan-in for the enrollment op (DC-568 spike). Selects the response rows at
/// <see cref="EnrollmentResponseMapping.Root"/> and decides each child's match via the named
/// <see cref="EnrollmentResponseMapping.Match"/> strategy, fanning verdicts back in by the echoed
/// correlation index (a 1-based request index):
///
/// <list type="bullet">
///   <item><see cref="EnrollmentMatchStrategy.AnyRowValueIn"/>: a child matches when ANY of its
///     candidate rows has the eligibility flag in the set (the original brick).</item>
///   <item><see cref="EnrollmentMatchStrategy.ConfidenceThreshold"/>: group a child's candidate rows
///     by index, take the MAX score, and match iff <c>max &gt; threshold</c> (STRICT — mirrors CO's
///     argmax + <c>&gt; threshold</c>). A missing/non-numeric score contributes nothing.</item>
/// </list>
///
/// HARD CAP: exactly these two NAMED strategies. The argmax + strict <c>&gt;</c> are code, never
/// config. A row whose index doesn't map to a requested child is ignored.
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

        JsonElement rows = SelectPath(root, mapping.Root);

        HashSet<string> matchedIndices = mapping.Match.Strategy switch
        {
            EnrollmentMatchStrategy.AnyRowValueIn => CorrelateAnyRowValueIn(mapping.Match, rows, indexField),
            EnrollmentMatchStrategy.ConfidenceThreshold =>
                CorrelateConfidenceThreshold(mapping.Match, rows, indexField),
            _ => throw new NotSupportedException(
                $"Unsupported enrollment match strategy '{mapping.Match.Strategy}'."),
        };

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
    /// and applies the named <see cref="EnrollmentResponseMapping.Match"/> strategy to it. No
    /// correlation index and no argmax — one call reads one child's verdict.
    /// </summary>
    public static bool EvaluateSingleResult(EnrollmentResponseMapping mapping, JsonElement root)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        JsonElement result = SelectPath(root, mapping.Root);

        return mapping.Match.Strategy switch
        {
            EnrollmentMatchStrategy.AnyRowValueIn => RowValueInSet(mapping.Match, result),
            EnrollmentMatchStrategy.ConfidenceThreshold => ScoreExceedsThreshold(mapping.Match, result),
            _ => throw new NotSupportedException(
                $"Unsupported enrollment match strategy '{mapping.Match.Strategy}'."),
        };
    }

    // Any-candidate fan-in: an index matches when ANY of its rows has the flag in the set.
    private static HashSet<string> CorrelateAnyRowValueIn(
        EnrollmentMatch match, JsonElement rows, string indexField)
    {
        var matchedIndices = new HashSet<string>(StringComparer.Ordinal);

        if (rows.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement row in rows.EnumerateArray())
            {
                string? index = ReadString(row, indexField);
                if (index is not null && RowValueInSet(match, row))
                {
                    matchedIndices.Add(index);
                }
            }
        }

        return matchedIndices;
    }

    // Argmax fan-in: group a child's rows by index, take the MAX score, match iff max > threshold
    // (STRICT). A missing/non-numeric score contributes nothing to its index's max.
    private static HashSet<string> CorrelateConfidenceThreshold(
        EnrollmentMatch match, JsonElement rows, string indexField)
    {
        var maxByIndex = new Dictionary<string, double>(StringComparer.Ordinal);

        if (rows.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement row in rows.EnumerateArray())
            {
                string? index = ReadString(row, indexField);
                if (index is null || !TryReadScore(match, row, out double score))
                {
                    continue;
                }

                if (!maxByIndex.TryGetValue(index, out double current) || score > current)
                {
                    maxByIndex[index] = score;
                }
            }
        }

        double threshold = match.Threshold!.Value;
        var matchedIndices = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string index, double max) in maxByIndex)
        {
            if (max > threshold)
            {
                matchedIndices.Add(index);
            }
        }

        return matchedIndices;
    }

    private static bool RowValueInSet(EnrollmentMatch match, JsonElement row)
    {
        string? value = ReadString(row, match.Field!);
        return value is not null && match.ValueIn!.Contains(value, StringComparer.Ordinal);
    }

    // A single result's score strictly exceeds the threshold. Missing/non-numeric → not a match.
    private static bool ScoreExceedsThreshold(EnrollmentMatch match, JsonElement result) =>
        TryReadScore(match, result, out double score) && score > match.Threshold!.Value;

    // Reads the score field as a number. A missing property or a non-numeric value is NOT a match.
    private static bool TryReadScore(EnrollmentMatch match, JsonElement record, out double score)
    {
        score = 0;

        if (record.ValueKind != JsonValueKind.Object
            || !record.TryGetProperty(match.ScoreField!, out JsonElement value))
        {
            return false;
        }

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.TryGetDouble(out score),
            JsonValueKind.String => double.TryParse(
                value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out score),
            _ => false,
        };
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
