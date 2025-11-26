using System;
using System.ComponentModel.DataAnnotations;

namespace SEBT.Portal.Core.AppSettings;

public class EmailOtpSenderServiceSettings
{
    [EmailAddress]
    public string SenderEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
     public string HtmlPreOtp { get; set; } = string.Empty; 
     public string HtmlPostOtp { get; set; } = string.Empty;
}
