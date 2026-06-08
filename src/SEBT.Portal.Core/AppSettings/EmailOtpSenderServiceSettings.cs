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
}
