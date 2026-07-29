using System.Text.Json;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Classifies a state-backend write response into a canonical <see cref="WriteOutcome"/>
/// (DC-568 spike; shared by the card-replacement and address-update paths). Evaluates the
/// classifier's ORDERED conditions first-match-wins; the first whose predicate holds selects the
/// outcome; none match → the classifier's default.
///
/// HARD CAP (write-side DSL-creep guard): each condition is EXACTLY ONE of three closed kinds —
/// HTTP status in a set, a body field's value in a set, or a body message containing any of a set
/// of substrings. No AND/OR combinators, no nesting. <see cref="Validate"/> enforces this
/// fail-loud at configuration time.
/// </summary>
internal static class WriteResultClassifier
{
    /// <summary>
    /// Validates the classifier's shape, fail-loud: every condition must set EXACTLY ONE of the
    /// three closed kinds, and the value/message kinds must name the body property they read.
    /// </summary>
    public static void Validate(ResultClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(classifier);

        foreach (ResultCondition condition in classifier.Conditions)
        {
            int kinds = 0;
            if (condition.StatusIn is not null)
            {
                kinds++;
            }

            if (condition.ValueIn is not null)
            {
                kinds++;
            }

            if (condition.MessageContains is not null)
            {
                kinds++;
            }

            if (kinds != 1)
            {
                throw new InvalidOperationException(
                    "Each write result condition must set exactly one of: statusIn, valueIn, messageContains.");
            }

            if (condition.ValueIn is not null && string.IsNullOrWhiteSpace(condition.Field))
            {
                throw new InvalidOperationException(
                    "A write 'valueIn' condition requires a body 'field' to read.");
            }

            if (condition.MessageContains is not null && string.IsNullOrWhiteSpace(condition.MessageField))
            {
                throw new InvalidOperationException(
                    "A write 'messageContains' condition requires a body 'messageField' to read.");
            }
        }
    }

    /// <summary>
    /// Classifies a response. <paramref name="body"/> is the parsed JSON root (or null when the
    /// backend returned no/invalid JSON — status-only conditions still apply).
    /// </summary>
    public static WriteOutcome Classify(ResultClassifier classifier, int statusCode, JsonElement? body)
    {
        foreach (ResultCondition condition in classifier.Conditions)
        {
            if (Matches(condition, statusCode, body))
            {
                return condition.Outcome;
            }
        }

        return classifier.Default;
    }

    private static bool Matches(ResultCondition condition, int statusCode, JsonElement? body)
    {
        if (condition.StatusIn is { } statuses)
        {
            return statuses.Contains(statusCode);
        }

        if (condition.ValueIn is { } values)
        {
            string? value = JsonRead.AsString(body, condition.Field!);
            return value is not null && values.Contains(value, StringComparer.Ordinal);
        }

        if (condition.MessageContains is { } needles)
        {
            string? message = JsonRead.AsString(body, condition.MessageField!);
            if (message is null)
            {
                return false;
            }

            string haystack = message.ToUpperInvariant();
            foreach (string needle in needles)
            {
                if (haystack.Contains(needle.ToUpperInvariant(), StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        return false;
    }
}
