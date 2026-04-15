using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration settings for Socure webhook endpoint rate limiting.
/// Uses IP-based partitioning with a fixed window.
/// TODO: Confirm appropriate thresholds with Socure and the team.
/// </summary>
public class WebhookRateLimitSettings
{
    public static readonly string SectionName = "WebhookRateLimitSettings";

    /// <summary>
    /// Maximum number of webhook requests allowed per window per IP address.
    /// </summary>
    [Range(1, 1000, ErrorMessage = "PermitLimit must be between 1 and 1000.")]
    public int PermitLimit { get; set; } = 60;

    /// <summary>
    /// Time window for rate limiting in minutes.
    /// </summary>
    [Range(0.1, 60.0, ErrorMessage = "WindowMinutes must be between 0.1 and 60.")]
    public double WindowMinutes { get; set; } = 1.0;
}
