using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration settings for OTP rate limiting.
/// </summary>
public class OtpRateLimitSettings
{
    public static readonly string SectionName = "OtpRateLimitSettings";

    /// <summary>
    /// Maximum number of OTP requests allowed per window.
    /// </summary>
    [Range(1, 100, ErrorMessage = "PermitLimit must be between 1 and 100.")]
    public int PermitLimit { get; set; } = 5;

    /// <summary>
    /// Time window for rate limiting in minutes.
    /// </summary>
    [Range(0.1, 60, ErrorMessage = "WindowMinutes must be between 0.1 and 60.")]
    public double WindowMinutes { get; set; } = 1.0;
}
