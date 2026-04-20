namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configures how long an ID proofing verification remains valid before the user
/// must re-verify. Expiration is computed dynamically from <c>IdProofingCompletedAt</c>
/// plus this duration — no baked-in expiration dates are stored.
/// </summary>
public class IdProofingValiditySettings
{
    public static readonly string SectionName = "IdProofingValidity";

    /// <summary>
    /// How long a completed ID proofing verification remains valid, in days.
    /// Default: 1826 days (~5 years). Use a low value in test environments to
    /// facilitate expiration testing.
    /// </summary>
    public int ValidityDays { get; set; } = 1826;
}
