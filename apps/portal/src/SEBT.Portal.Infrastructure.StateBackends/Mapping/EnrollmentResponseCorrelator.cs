using System.Globalization;
using System.Text.Json;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Response-side fan-in: selects the rows at <see cref="EnrollmentResponseMapping.Root"/> and
/// decides each child's match via the named <see cref="EnrollmentResponseMapping.Match"/> strategy,
/// fanning verdicts back in by the echoed 1-based correlation index. The argmax and the strict
/// <c>&gt;</c> in confidence matching live here, not in config.
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

        JsonElement rows = JsonPathSelector.Select(root, mapping.Root);

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

        JsonElement result = JsonPathSelector.Select(root, mapping.Root);

        return mapping.Match.Strategy switch
        {
            EnrollmentMatchStrategy.AnyRowValueIn => RowValueInSet(mapping.Match, result),
            EnrollmentMatchStrategy.ConfidenceThreshold => ScoreExceedsThreshold(mapping.Match, result),
            _ => throw new NotSupportedException(
                $"Unsupported enrollment match strategy '{mapping.Match.Strategy}'."),
        };
    }

    // An index matches when any of its rows has the flag in the set.
    private static HashSet<string> CorrelateAnyRowValueIn(
        EnrollmentMatch match, JsonElement rows, string indexField)
    {
        var matchedIndices = new HashSet<string>(StringComparer.Ordinal);

        if (rows.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement row in rows.EnumerateArray())
            {
                string? index = JsonRead.AsString(row, indexField);
                if (index is not null && RowValueInSet(match, row))
                {
                    matchedIndices.Add(index);
                }
            }
        }

        return matchedIndices;
    }

    // Per index, take the max score and match iff max > threshold (strict). A missing/non-numeric
    // score contributes nothing.
    private static HashSet<string> CorrelateConfidenceThreshold(
        EnrollmentMatch match, JsonElement rows, string indexField)
    {
        var maxByIndex = new Dictionary<string, double>(StringComparer.Ordinal);

        if (rows.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement row in rows.EnumerateArray())
            {
                string? index = JsonRead.AsString(row, indexField);
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
        string? value = JsonRead.AsString(row, match.Field!);
        return value is not null && match.ValueIn!.Contains(value, StringComparer.Ordinal);
    }

    private static bool ScoreExceedsThreshold(EnrollmentMatch match, JsonElement result) =>
        TryReadScore(match, result, out double score) && score > match.Threshold!.Value;

    // Reads the score as a number; a missing property or non-numeric value is not a match.
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

}
