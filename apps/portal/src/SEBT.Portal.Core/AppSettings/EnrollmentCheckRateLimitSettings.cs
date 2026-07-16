using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration settings for enrollment check rate limiting.
/// </summary>
public class EnrollmentCheckRateLimitSettings
{
    public static readonly string SectionName = "EnrollmentCheckRateLimitSettings";

    /// <summary>
    /// Maximum number of enrollment check requests allowed per window per IP address.
    /// </summary>
    [Range(1, 100, ErrorMessage = "PermitLimit must be between 1 and 100.")]
    public int PermitLimit { get; set; } = 10;

    /// <summary>
    /// Time window for rate limiting in minutes.
    /// </summary>
    [Range(0.1, 60.0, ErrorMessage = "WindowMinutes must be between 0.1 and 60.")]
    public double WindowMinutes { get; set; } = 1.0;
}
