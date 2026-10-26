namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configures the OIDC claim names used by the external identity provider to convey
/// ID verification level and date, under <c>Oidc:VerificationClaims</c>.
/// The primary names are required whenever OIDC is enabled; the fallbacks are optional.
/// </summary>
public class OidcVerificationClaimSettings : IHaveConfigSectionName
{
    public static string SectionName => "Oidc:VerificationClaims";

    /// <summary>
    /// OIDC claim name whose value indicates the user's verification level.
    /// Expected values: "1.5" → IAL1plus. When this claim is absent, empty, or not a recognized
    /// level, the translator falls back to <see cref="FallbackLevelClaimName"/> if one is set.
    /// </summary>
    public string? LevelClaimName { get; set; }

    /// <summary>
    /// OIDC claim name whose value is the ISO 8601 date/time when verification was completed.
    /// When this claim is absent or not parseable as a date, the translator falls back to
    /// <see cref="FallbackDateClaimName"/> if one is set.
    /// </summary>
    public string? DateClaimName { get; set; }

    /// <summary>
    /// Secondary claim name for verification level when <see cref="LevelClaimName"/> is absent or unusable.
    /// Unset means no fallback.
    /// </summary>
    public string? FallbackLevelClaimName { get; set; }

    /// <summary>
    /// Secondary claim name for verification completion date when <see cref="DateClaimName"/> is absent or unusable.
    /// Unset means no fallback.
    /// </summary>
    public string? FallbackDateClaimName { get; set; }
}
