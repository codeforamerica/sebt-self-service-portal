using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Infrastructure.Services
{
    /// <summary>
    /// Service responsible for sending One-Time Password (OTP) codes via email.
    /// </summary>
    /// <remarks>
    /// This service uses SMTP to send HTML-formatted emails containing OTP codes
    /// for user authentication purposes.
    /// </remarks>
    /// <param name="optionsMonitor">The options monitor for email sender settings.</param>
    /// <param name="smtpClientService">The SMTP client service used to send emails.</param>
    /// <param name="logger">The logger for recording email sending operations.</param>
    public class EmailOtpSenderService(
        IOptionsMonitor<EmailOtpSenderServiceSettings> optionsMonitor,
        ILogger<EmailOtpSenderService> logger,
        ISmtpClientService smtpClientService) : IOtpSenderService
    {
        private readonly EmailOtpSenderServiceSettings settings = optionsMonitor.CurrentValue;

        public async Task<Result> SendOtpAsync(string to, string otp)
        {
            try
            {
                // Send the email        
                await smtpClientService.SendEmailAsync(
                    to,
                    settings.SenderEmail,
                    settings.Subject,
                    $"{settings.HtmlPreOtp}{otp}{settings.HtmlPostOtp}");
                    
                logger.LogInformation("OTP email sent to {To}", to);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, $"Failed to send OTP to: {to}");
                return new PreconditionFailedResult(PreconditionFailedReason.Conflict, $"Failed to send OTP to: {to}");
            }

            return new SuccessResult();
        }
    }
}
