using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

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
    private readonly SmtpClientSettings _smtpClientSettings = optionsMonitor.CurrentValue;

    public Task SendEmailAsync(string to, string from, string subject, string body)
    {
        return SendEmailAsync(to, from, subject, body, []);
    }

    public async Task SendEmailAsync(string to, string from, string subject, string body, IEnumerable<EmailLinkedResource> linkedResources)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(from),
            Subject = subject,
            IsBodyHtml = true
        };
        message.To.Add(new MailAddress(to));

        var htmlView = AlternateView.CreateAlternateViewFromString(body, null, MediaTypeNames.Text.Html);
        try
        {
            foreach (var resource in linkedResources)
            {
                var stream = new MemoryStream(resource.Data);
                var linkedResource = new LinkedResource(stream, resource.ContentType)
                {
                    ContentId = resource.ContentId
                };
                htmlView.LinkedResources.Add(linkedResource);
            }

            message.AlternateViews.Add(htmlView);
        }
        catch
        {
            htmlView.Dispose();
            throw;
        }

        using var smtpClient = new SmtpClient(_smtpClientSettings.SmtpServer, _smtpClientSettings.SmtpPort)
        {
            EnableSsl = _smtpClientSettings.EnableSsl
        };

        if (!string.IsNullOrEmpty(_smtpClientSettings.UserName) && _smtpClientSettings.Password != null)
        {
            smtpClient.Credentials = new NetworkCredential(_smtpClientSettings.UserName, _smtpClientSettings.Password);
        }

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
