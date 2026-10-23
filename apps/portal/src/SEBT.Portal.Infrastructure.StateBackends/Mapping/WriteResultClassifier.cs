using System.Text.Json;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// A classified write outcome plus the backend-supplied message, when one was readable from the
/// response body.
/// </summary>
internal readonly record struct WriteClassification(WriteOutcome Outcome, string? Message);

/// <summary>
/// Classifies a write response into a canonical <see cref="WriteOutcome"/>: ordered conditions,
/// first-match-wins, falling back to the default.
/// </summary>
internal static class WriteResultClassifier
{
    /// <summary>
    /// Fails loud at load unless each condition sets exactly one closed kind and value/message
    /// kinds name the body property they read.
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
    /// Classifies a response. <paramref name="body"/> is null when the backend returned no/invalid
    /// JSON — status-only conditions still apply.
    /// </summary>
    public static WriteClassification Classify(ResultClassifier classifier, int statusCode, JsonElement? body)
    {
        foreach (ResultCondition condition in classifier.Conditions)
        {
            if (Matches(condition, statusCode, body))
            {
                return new WriteClassification(condition.Outcome, ReadMessage(classifier, condition, body));
            }
        }

        return new WriteClassification(classifier.Default, ReadMessage(classifier, matched: null, body));
    }

    // The matched condition's messageField, else the first messageField any condition declares —
    // so a default-classified error still surfaces the backend's message.
    private static string? ReadMessage(ResultClassifier classifier, ResultCondition? matched, JsonElement? body)
    {
        string? messageField = matched?.MessageField
            ?? classifier.Conditions
                .FirstOrDefault(condition => !string.IsNullOrWhiteSpace(condition.MessageField))?
                .MessageField;

        if (messageField is null || body is null)
        {
            return null;
        }

        string? message = JsonRead.AsString(body, messageField);
        return string.IsNullOrWhiteSpace(message) ? null : message;
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
