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
    /// How long a completed ID proofing verification remains valid, in years.
    /// Default: 5 years.
    /// </summary>
    public double ValidityYears { get; set; } = 5;
}
