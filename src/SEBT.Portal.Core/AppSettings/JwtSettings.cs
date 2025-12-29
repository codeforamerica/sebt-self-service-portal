using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration settings for JWT token generation.
/// </summary>
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
    /// Token expiration time in minutes.
    /// </summary>
    [Range(1, 1440, ErrorMessage = "ExpirationMinutes must be between 1 and 1440 (24 hours).")]
    public int ExpirationMinutes { get; set; } = 60;
}

