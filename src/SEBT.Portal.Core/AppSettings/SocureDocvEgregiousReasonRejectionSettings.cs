using System.ComponentModel;

namespace SEBT.Portal.Core.AppSettings;

/// <summary>
/// Configurable immediate rejection for known egregious Socure DocV reason codes
/// (device tampering, liveness failure, etc.)
/// </summary>
public class SocureDocvEgregiousReasonRejectionSettings
{
    /// <summary>
    /// When true, matching DocV reason codes reject the user immediately without routing
    /// to DocV step-up. The user may retry ID proofing with different information.
    /// </summary>
    [DefaultValue(false)]
    public bool Enabled { get; set; }

    /// <summary>
    /// Socure DocV reason codes that trigger immediate rejection when present on an evaluation
    /// or terminal webhook. Case-insensitive match.
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
