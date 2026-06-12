using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configurable cooldown applied when Socure DocV returns known egregious reason codes
/// (e.g. device tampering, liveness failure). DC enables this; CO leaves it disabled.
/// </summary>
public class SocureDocvEgregiousReasonCooldownSettings
{
    /// <summary>
    /// When true, matching DocV reason codes trigger a portal-side cooldown that blocks
    /// new ID proofing and DocV retries until <see cref="CooldownDays"/> elapse.
    /// </summary>
    [DefaultValue(false)]
    public bool Enabled { get; set; }

    /// <summary>
    /// How long the user must wait before attempting ID proofing / DocV again.
    /// </summary>
    [Range(1, 365, ErrorMessage = "CooldownDays must be between 1 and 365.")]
    [DefaultValue(14)]
    public int CooldownDays { get; set; } = 14;

    /// <summary>
    /// Socure DocV reason codes that trigger the cooldown when present on a terminal
    /// evaluation webhook. Case-insensitive match.
    /// </summary>
    public List<string> ReasonCodes { get; set; } =
    [
        "R815", // Device Tampering or Injection Risk
        "R819", // Digital Image or Paper Image
        "R820", // Headshot on Doc Invalid
        "R827", // Doc Expired
        "R834", // Selfie Doesn't pass Liveness
        "R836", // Facial Match Failure
        "R845"  // Failed Minimum age
    ];
}
