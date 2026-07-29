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

        Dictionary<string, ChildVerdict> verdicts = mapping.Match.Strategy switch
        {
            EnrollmentMatchStrategy.AnyRowValueIn => CorrelateAnyRowValueIn(mapping, rows, indexField),
            EnrollmentMatchStrategy.ConfidenceThreshold =>
                CorrelateConfidenceThreshold(mapping, rows, indexField),
            _ => throw new NotSupportedException(
                $"Unsupported enrollment match strategy '{mapping.Match.Strategy}'."),
        };

        var results = new List<EnrollmentChildResult>(request.Children.Count);
        for (int i = 0; i < request.Children.Count; i++)
        {
            string index = (i + 1).ToString(CultureInfo.InvariantCulture);
            ChildVerdict verdict = verdicts.GetValueOrDefault(index, ChildVerdict.None);
            results.Add(new EnrollmentChildResult(
                request.Children[i].CheckId, verdict.IsMatch, verdict.MatchConfidence, verdict.StatusMessage));
        }

        return new EnrollmentCheckResult(results, ReadResultMessage(mapping, root));
    }

    /// <summary>
    /// PerChild evaluation: selects the single result object at <see cref="EnrollmentResponseMapping.Root"/>
    /// and applies the named <see cref="EnrollmentResponseMapping.Match"/> strategy to it. No
    /// correlation index and no argmax — one call reads one child's verdict, and the single result
    /// object supplies the confidence and status-message carriers.
    /// </summary>
    public static EnrollmentChildResult EvaluateSingleResult(
        EnrollmentResponseMapping mapping, JsonElement root, string checkId)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        JsonElement result = JsonPathSelector.Select(root, mapping.Root);

        bool isMatch = mapping.Match.Strategy switch
        {
            EnrollmentMatchStrategy.AnyRowValueIn => RowValueInSet(mapping.Match, result),
            EnrollmentMatchStrategy.ConfidenceThreshold => ConfidenceThresholdMatches(mapping.Match, result),
            _ => throw new NotSupportedException(
                $"Unsupported enrollment match strategy '{mapping.Match.Strategy}'."),
        };

        // Confidence only exists under confidenceThreshold; like the batch path, it is reported
        // even on the sub-threshold non-match path.
        double? confidence =
            mapping.Match.Strategy == EnrollmentMatchStrategy.ConfidenceThreshold
                && TryReadScore(mapping.Match, result, out double score)
            ? score
            : null;

        return new EnrollmentChildResult(checkId, isMatch, confidence, ReadStatusMessage(mapping, result));
    }

    /// <summary>
    /// Reads the result-level message (when a <c>messageField</c> is configured) from the response
    /// document root — the parent of <see cref="EnrollmentResponseMapping.Root"/>'s rows, not a row.
    /// </summary>
    public static string? ReadResultMessage(EnrollmentResponseMapping mapping, JsonElement root)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        return mapping.MessageField is null
            ? null
            : JsonRead.AsString(JsonPathSelector.Select(root, mapping.MessageField));
    }

    // A child's fan-in verdict plus the optional carriers its winning row supplied. `None` is the
    // verdict for a child the response carried no (matching/scored) rows for.
    private sealed record ChildVerdict(bool IsMatch, double? MatchConfidence, string? StatusMessage)
    {
        public static readonly ChildVerdict None = new(false, null, null);
    }

    // An index matches when any of its rows has the flag in the set; the FIRST matching row is the
    // winner and supplies the status message. No score field, so confidence is always null.
    private static Dictionary<string, ChildVerdict> CorrelateAnyRowValueIn(
        EnrollmentResponseMapping mapping, JsonElement rows, string indexField)
    {
        var verdicts = new Dictionary<string, ChildVerdict>(StringComparer.Ordinal);

        if (rows.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement row in rows.EnumerateArray())
            {
                string? index = JsonRead.AsString(row, indexField);
                if (index is not null && !verdicts.ContainsKey(index) && RowValueInSet(mapping.Match, row))
                {
                    verdicts[index] = new ChildVerdict(
                        IsMatch: true, MatchConfidence: null, ReadStatusMessage(mapping, row));
                }
            }
        }

        return verdicts;
    }

    // Per index, take the argmax row and match iff its score > threshold (strict) AND it passes the
    // optional eligibility check. A missing/non-numeric score contributes nothing.
    private static Dictionary<string, ChildVerdict> CorrelateConfidenceThreshold(
        EnrollmentResponseMapping mapping, JsonElement rows, string indexField)
    {
        EnrollmentMatch match = mapping.Match;
        var bestByIndex = new Dictionary<string, (double Score, JsonElement Row)>(StringComparer.Ordinal);

        if (rows.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement row in rows.EnumerateArray())
            {
                string? index = JsonRead.AsString(row, indexField);
                if (index is null || !TryReadScore(match, row, out double score))
                {
                    continue;
                }

                if (!bestByIndex.TryGetValue(index, out (double Score, JsonElement Row) current)
                    || score > current.Score)
                {
                    bestByIndex[index] = (score, row);
                }
            }
        }

        double threshold = match.Threshold!.Value;
        var verdicts = new Dictionary<string, ChildVerdict>(StringComparer.Ordinal);
        foreach ((string index, (double score, JsonElement row)) in bestByIndex)
        {
            // Eligibility is read from the argmax row only — a lower-scoring eligible candidate
            // cannot rescue an ineligible best row. The argmax row's carriers are reported even on
            // the non-match path (mirrors the CO plugin, so callers can surface the computed score).
            bool isMatch = score > threshold && PassesEligibility(match, row);
            verdicts[index] = new ChildVerdict(isMatch, score, ReadStatusMessage(mapping, row));
        }

        return verdicts;
    }

    // The winning row's status text, when a statusMessageField is configured.
    private static string? ReadStatusMessage(EnrollmentResponseMapping mapping, JsonElement row) =>
        mapping.StatusMessageField is null ? null : JsonRead.AsString(row, mapping.StatusMessageField);

    private static bool RowValueInSet(EnrollmentMatch match, JsonElement row)
    {
        string? value = JsonRead.AsString(row, match.Field!);
        return value is not null && match.ValueIn!.Contains(value, StringComparer.Ordinal);
    }

    // The optional eligibility check on confidenceThreshold: when field/valueIn are configured,
    // the row must ALSO carry an eligible flag value (fixed AND — config only names the params).
    private static bool PassesEligibility(EnrollmentMatch match, JsonElement row) =>
        match.Field is null || RowValueInSet(match, row);

    // PerChild confidenceThreshold: the single result carries both the score and (when configured)
    // the eligibility flag.
    private static bool ConfidenceThresholdMatches(EnrollmentMatch match, JsonElement result) =>
        TryReadScore(match, result, out double score)
            && score > match.Threshold!.Value
            && PassesEligibility(match, result);

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
