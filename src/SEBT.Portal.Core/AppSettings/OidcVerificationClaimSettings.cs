namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configures the OIDC claim names used by the external identity provider to convey
/// ID verification level and date. Defaults match Colorado's PingOne/Socure integration.
/// States whose IdP uses different claim names can override via <c>Oidc:VerificationClaims</c>.
/// </summary>
public class OidcVerificationClaimSettings
{
    public static readonly string SectionName = "Oidc:VerificationClaims";

    /// <summary>
    /// OIDC claim name whose value indicates the user's verification level.
    /// Expected values: "1.5" → IAL1plus. Default: "socureIdVerificationLevel".
    /// </summary>
    public string LevelClaimName { get; set; } = "socureIdVerificationLevel";

    /// <summary>
    /// OIDC claim name whose value is the ISO 8601 date/time when verification was completed.
    /// Default: "socureIdVerificationDate".
    /// </summary>
    public string DateClaimName { get; set; } = "socureIdVerificationDate";
}
