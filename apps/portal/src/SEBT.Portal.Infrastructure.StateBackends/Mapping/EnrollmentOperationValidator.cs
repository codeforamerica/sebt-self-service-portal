using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Infrastructure.StateBackends.Mapping;

/// <summary>
/// Fail-loud validation of the enrollment op's call-mode / index-field / expand combination
/// (DC-568 spike). Invoked before dispatch so a misconfigured op fails on first use rather than
/// silently taking the wrong path.
///
/// <list type="bullet">
///   <item><see cref="EnrollmentCallMode.Batch"/> REQUIRES an <c>indexField</c> on both the request
///     binding and the response mapping — rows are correlated by that echoed index.</item>
///   <item><see cref="EnrollmentCallMode.PerChild"/> must NOT set an <c>indexField</c> on either side —
///     each call is a single child, so there is nothing to correlate.</item>
///   <item><see cref="EnrollmentCallMode.PerChild"/> combined with a candidate
///     <see cref="EnrollmentRequestBinding.Expand"/> is NOT supported yet — no real state needs it,
///     so the combo is refused rather than built.</item>
/// </list>
/// </summary>
internal static class EnrollmentOperationValidator
{
    public static void Validate(
        EnrollmentCallMode callMode, EnrollmentRequestBinding binding, EnrollmentResponseMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(mapping);

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
}
