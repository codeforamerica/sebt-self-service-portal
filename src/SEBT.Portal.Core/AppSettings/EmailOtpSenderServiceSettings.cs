using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

public class EmailOtpSenderServiceSettings
{
    public static readonly string SectionName = "EmailOtpSenderServiceSettings";

    /// <summary>
    /// The email address that OTP emails will be sent from.
    /// </summary>
    [EmailAddress]
    public required string SenderEmail { get; set; }

    /// <summary>
    /// The display name for the sender (e.g., "DC SUN Bucks").
    /// </summary>
    public required string SenderName { get; set; }

    /// <summary>
    /// The email subject line (e.g., "Your DC SUN Bucks Login Code").
    /// </summary>
    public required string Subject { get; set; }

    /// <summary>
    /// The program name displayed in the email body (e.g., "DC SUN Bucks").
    /// </summary>
    public required string ProgramName { get; set; }

    /// <summary>
    /// The state and program name for display purposes (e.g., "DC SUN Bucks").
    /// </summary>
    public required string StateName { get; set; }

    /// <summary>
    /// Number of minutes until the OTP code expires.
    /// </summary>
    public int ExpiryMinutes { get; set; } = 10;

    /// <summary>
    /// The language code for the email (e.g., "en", "es").
    /// Used for the HTML lang attribute for accessibility.
    /// </summary>
    public string Language { get; set; } = "en";
}
