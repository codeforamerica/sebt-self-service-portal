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
    /// When this claim is absent, empty, or not a recognized level, the translator may fall back to
    /// <see cref="FallbackLevelClaimName"/>
    /// </summary>
    public string LevelClaimName { get; set; } = "socureIdVerificationLevel";

    /// <summary>
    /// OIDC claim name whose value is the ISO 8601 date/time when verification was completed.
    /// Default: "socureIdVerificationDate".
    /// When this claim is absent or not parseable as a date, the translator may fall back to
    /// <see cref="FallbackDateClaimName"/> 
    /// </summary>
    public string DateClaimName { get; set; } = "socureIdVerificationDate";

    /// <summary>
    /// Secondary claim name for verification level when <see cref="LevelClaimName"/> is absent or unusable.
    /// Defaults to myColorado&apos;s <c>myCoIdVerificationLevel</c> when omitted or whitespace.
    /// </summary>
    public string? FallbackLevelClaimName { get; set; }

    /// <summary>
    /// Secondary claim name for verification completion date when <see cref="DateClaimName"/> is absent or unusable.
    /// Defaults to myColorado&apos;s <c>myCoIdVerificationDate</c> when omitted or whitespace.
    /// </summary>
    public string? FallbackDateClaimName { get; set; }
}
