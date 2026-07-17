using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configuration settings for rate limiting the public enrollment checker features
/// endpoint. Deliberately a separate policy from <see cref="EnrollmentCheckRateLimitSettings"/>:
/// every open checker tab polls the features endpoint once a minute, so sharing the
/// enrollment-check partition would let a few tabs behind one NAT (school computer lab,
/// library) drain the per-IP budget that real enrollment checks need.
/// </summary>
public class CheckerFeaturesRateLimitSettings
{
    public static readonly string SectionName = "CheckerFeaturesRateLimitSettings";

    /// <summary>
    /// Maximum number of features requests allowed per window per IP address.
    /// Generous relative to the one-poll-per-minute-per-tab cadence; a 429 only
    /// delays banner pickup by a poll cycle.
    /// </summary>
    [Range(1, 1000, ErrorMessage = "PermitLimit must be between 1 and 1000.")]
    public int PermitLimit { get; set; } = 30;

    /// <summary>
    /// Time window for rate limiting in minutes.
    /// </summary>
    [Range(0.1, 60.0, ErrorMessage = "WindowMinutes must be between 0.1 and 60.")]
    public double WindowMinutes { get; set; } = 1.0;
}
