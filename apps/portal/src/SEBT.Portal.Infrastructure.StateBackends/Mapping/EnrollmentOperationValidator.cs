using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Fail-loud validation of the enrollment op's call-mode / index-field / expand / match combination,
/// so a misconfigured op fails at load rather than silently taking the wrong dispatch path.
/// </summary>
internal static class EnrollmentOperationValidator
{
    public static void Validate(
        EnrollmentCallMode callMode, EnrollmentRequestBinding binding, EnrollmentResponseMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(mapping);

        ValidateMatch(mapping.Match);

        switch (callMode)
        {
            case EnrollmentCallMode.Batch:
                if (string.IsNullOrEmpty(binding.IndexField) || string.IsNullOrEmpty(mapping.IndexField))
                {
                    throw new InvalidOperationException(
                        "Enrollment callMode 'Batch' requires an indexField on both the request binding "
                        + "and the response mapping so rows can be correlated back to children.");
                }

                break;

            case EnrollmentCallMode.PerChild:
                if (!string.IsNullOrEmpty(binding.IndexField) || !string.IsNullOrEmpty(mapping.IndexField))
                {
                    throw new InvalidOperationException(
                        "Enrollment callMode 'PerChild' must NOT set an indexField: each call is a single "
                        + "child, so there is no correlation index.");
                }

                if (binding.Expand != CandidateExpansion.None)
                {
                    throw new InvalidOperationException(
                        "Enrollment callMode 'PerChild' with candidate expansion is not supported yet.");
                }

                break;

            default:
                throw new NotSupportedException($"Unsupported enrollment callMode '{callMode}'.");
        }
    }

    // Fail loud when the strategy is missing its required params. The comparison lives in code;
    // config only names the strategy.
    private static void ValidateMatch(EnrollmentMatch match)
    {
        switch (match.Strategy)
        {
            case EnrollmentMatchStrategy.AnyRowValueIn:
                if (string.IsNullOrEmpty(match.Field) || match.ValueIn is not { Count: > 0 })
                {
                    throw new InvalidOperationException(
                        "Enrollment match strategy 'AnyRowValueIn' requires a 'field' and a non-empty 'valueIn'.");
                }

                break;

            case EnrollmentMatchStrategy.ConfidenceThreshold:
                if (string.IsNullOrEmpty(match.ScoreField) || match.Threshold is null)
                {
                    throw new InvalidOperationException(
                        "Enrollment match strategy 'ConfidenceThreshold' requires a 'scoreField' and a 'threshold'.");
                }

                // The optional eligibility check is field + valueIn TOGETHER or neither — one alone
                // would silently degrade to score-only matching.
                bool hasField = !string.IsNullOrEmpty(match.Field);
                bool hasValueIn = match.ValueIn is { Count: > 0 };
                if (hasField != hasValueIn)
                {
                    throw new InvalidOperationException(
                        "Enrollment match strategy 'ConfidenceThreshold' takes 'field' and a non-empty "
                        + "'valueIn' together or not at all.");
                }

                break;

            default:
                throw new NotSupportedException(
                    $"Unsupported enrollment match strategy '{match.Strategy}'.");
        }
    }
}
