using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Utilities;

/// <summary>
/// Shared logic for Socure DocV egregious-reason cooldown enforcement.
/// </summary>
public static class SocureDocvEgregiousReasonCooldown
{
    public const string OffboardingReason = "docVerificationCooldown";

    /// <summary>
    /// Returns true when the user is still within an active DocV egregious-reason cooldown window.
    /// </summary>
    public static bool IsUserInCooldown(User user, DateTime utcNow)
    {
        return user.IdProofingCooldownUntil.HasValue && user.IdProofingCooldownUntil.Value > utcNow;
    }

    /// <summary>
    /// Returns the configured egregious reason codes present on the DocV webhook, if any.
    /// </summary>
    public static IReadOnlyList<string>? GetMatchingEgregiousCodes(
        SocureDocvEgregiousReasonCooldownSettings settings,
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

    /// <summary>
    /// Computes the cooldown end timestamp, preserving an existing longer cooldown if present.
    /// </summary>
    public static DateTime ComputeCooldownUntil(
        SocureDocvEgregiousReasonCooldownSettings settings,
        User user,
        DateTime utcNow)
    {
        var candidate = utcNow.AddDays(settings.CooldownDays);
        if (user.IdProofingCooldownUntil.HasValue && user.IdProofingCooldownUntil.Value > candidate)
        {
            return user.IdProofingCooldownUntil.Value;
        }

        return candidate;
    }
}
