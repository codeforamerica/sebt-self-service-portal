using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration settings for JWT token generation.
/// </summary>
/// <remarks>
/// Idle and absolute timeouts together implement the session lifetime policy described in
/// OWASP Session Management and NIST SP 800-63B (§7.1, IAL2 sessions: ≤30 min idle,
/// ≤12 hr absolute). The portal's defaults are tighter than the NIST ceiling.
/// </remarks>
public class JwtSettings
{
    public static readonly string SectionName = "JwtSettings";

    /// <summary>
    /// The secret key used to sign JWT tokens.
    /// </summary>
    [Required(ErrorMessage = "SecretKey is required.")]
    [MinLength(32, ErrorMessage = "SecretKey must be at least 32 characters long.")]
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// The issuer of the JWT token.
    /// </summary>
    [Required(ErrorMessage = "Issuer is required.")]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// The audience of the JWT token.
    /// </summary>
    [Required(ErrorMessage = "Audience is required.")]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Sliding (idle) session lifetime, in minutes. The session cookie is renewed on
    /// activity-driven refresh; an idle session expires when this window elapses.
    /// </summary>
    [Range(1, 1440, ErrorMessage = "ExpirationMinutes must be between 1 and 1440 (24 hours).")]
    public int ExpirationMinutes { get; set; } = 15;

    /// <summary>
    /// Absolute session lifetime, in minutes, measured from the user's original
    /// authentication time (<c>auth_time</c> claim). Refresh requests are rejected
    /// once this cap is reached, regardless of activity. Must be greater than or
    /// equal to <see cref="ExpirationMinutes"/> — enforced by JwtSettingsValidator.
    /// </summary>
    [Range(1, 1440, ErrorMessage = "AbsoluteExpirationMinutes must be between 1 and 1440 (24 hours).")]
    public int AbsoluteExpirationMinutes { get; set; } = 60;
}
