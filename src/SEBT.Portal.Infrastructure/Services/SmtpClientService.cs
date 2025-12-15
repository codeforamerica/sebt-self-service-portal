using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services
{
    /// <summary>
    /// Service for sending emails using SMTP protocol.
    /// </summary>
    /// <remarks>
    /// This service wraps the <see cref="SmtpClient"/> to provide email sending capabilities
    /// with configuration from <see cref="SmtpClientSettings"/>.
    /// </remarks>
    /// <param name="optionsMonitor">Options monitor for SMTP client settings.</param>
    /// <param name="logger">Logger instance for logging email operations.</param>
    public class SmtpClientService(IOptionsMonitor<SmtpClientSettings> optionsMonitor, ILogger<SmtpClientService> logger)
        : ISmtpClientService
    {
        private readonly SmtpClientSettings smtpClientSettings = optionsMonitor.CurrentValue;

        public async Task SendEmailAsync(string to, string from, string subject, string body, bool isBodyHtml = true)
        {
            using var message = new MailMessage
            {
                From = new MailAddress(from),
                Subject = subject,
                Body = body,
                IsBodyHtml = isBodyHtml
            };
            message.To.Add(new MailAddress(to));

            // Configure the SMTP client
            using var smtpClient = new SmtpClient(smtpClientSettings.SmtpServer, smtpClientSettings.SmtpPort)
            {
                EnableSsl = smtpClientSettings.EnableSsl
            };

            // Send the email        
            try
            {
                await smtpClient.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error sending email to: {Recipients}", message.To);
                throw;
            }
        }
    }
}
