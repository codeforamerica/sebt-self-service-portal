using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

public class EmailOtpSenderServiceSettings
{
    public static readonly string SectionName = "EmailOtpSenderServiceSettings";
   
    [EmailAddress]
    public required string SenderEmail { get; init; }
    public required string Subject { get; init; }
    public required string HtmlPreOtp { get; init; }
    public required string HtmlPostOtp { get; init; }
}
