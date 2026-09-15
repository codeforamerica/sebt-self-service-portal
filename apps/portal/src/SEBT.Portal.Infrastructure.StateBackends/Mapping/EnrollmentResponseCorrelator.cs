using System.Globalization;
using System.Text.Json;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Response-side fan-in: decides each child's match and fans verdicts back in by the echoed 1-based
/// correlation index. The argmax and the strict <c>&gt;</c> in confidence matching live here, not in config.
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
    /// PerChild evaluation: applies the match strategy to the single result object at
    /// <see cref="EnrollmentResponseMapping.Root"/> — no correlation index, no argmax.
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

        // Like the batch path, confidence is reported even on the sub-threshold non-match path.
        double? confidence =
            mapping.Match.Strategy == EnrollmentMatchStrategy.ConfidenceThreshold
                && TryReadScore(mapping.Match, result, out double score)
            ? score
            : null;

        return new EnrollmentChildResult(checkId, isMatch, confidence, ReadStatusMessage(mapping, result));
    }

    /// <summary>Reads the configured <c>messageField</c> from the response document root — not a row.</summary>
    public static string? ReadResultMessage(EnrollmentResponseMapping mapping, JsonElement root)
    {
        ArgumentNullException.ThrowIfNull(mapping);

        return mapping.MessageField is null
            ? null
            : JsonRead.AsString(JsonPathSelector.Select(root, mapping.MessageField));
    }

    // A child's fan-in verdict; None means the response carried no matching/scored rows for it.
    private sealed record ChildVerdict(bool IsMatch, double? MatchConfidence, string? StatusMessage)
    {
        public static readonly ChildVerdict None = new(false, null, null);
    }

    // The FIRST matching row wins and supplies the status message; no score field, so confidence
    // is always null.
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
            // cannot rescue it. Its carriers are reported even on the non-match path (CO-plugin parity).
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

    // The optional eligibility check on confidenceThreshold: a fixed AND — config only names the params.
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
