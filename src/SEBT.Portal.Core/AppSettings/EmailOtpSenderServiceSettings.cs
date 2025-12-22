using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

public class EmailOtpSenderServiceSettings
{
    public static readonly string SectionName = "EmailOtpSenderServiceSettings";

    [EmailAddress]
    public required string SenderEmail { get; set; }
    public required string Subject { get; set; }
    public required string HtmlPreOtp { get; set; }
    public required string HtmlPostOtp { get; set; }
}
