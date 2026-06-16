using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Core.Utilities;

/// <summary>
/// Shared logic for Socure DocV egregious-reason immediate rejection.
/// </summary>
public static class SocureDocvEgregiousReasonCodes
{
    /// <summary>
    /// Off-boarding reason when DocV fails with an egregious reason code. User may retry ID proofing.
    /// </summary>
    public const string OffboardingReason = "docVerificationFailed";

    /// <summary>
    /// Returns the configured egregious reason codes present on the DocV payload, if any.
    /// </summary>
    public static IReadOnlyList<string>? GetMatchingEgregiousCodes(
        SocureDocvEgregiousReasonRejectionSettings settings,
        IReadOnlyList<string>? documentVerificationReasonCodes)
    {
        if (!settings.Enabled
            || documentVerificationReasonCodes == null
            || documentVerificationReasonCodes.Count == 0
            || settings.ReasonCodes.Count == 0)
        {
            return null;
        }

        var configured = new HashSet<string>(
            settings.ReasonCodes,
            StringComparer.OrdinalIgnoreCase);

        var matches = documentVerificationReasonCodes
            .Where(code => configured.Contains(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return matches.Count > 0 ? matches : null;
    }
}
