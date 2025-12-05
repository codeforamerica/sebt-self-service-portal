

using System.Net.Mail;
using Microsoft.Extensions.Options;
using MimeKit;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

public class MailKitClientService(IOptionsMonitor<SmtpClientSettings> optionsMonitor) : ISmtpClientService
{
    private readonly SmtpClientSettings _smtpSettings = optionsMonitor.CurrentValue;
    public Task SendEmailAsync(string to, string from, string subject, string body, bool isBodyHtml = true)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(from, from));
        message.To.Add(new MailboxAddress(to, to));
        message.Subject = subject;

        message.Body = new TextPart(isBodyHtml? MimeKit.Text.TextFormat.Html : MimeKit.Text.TextFormat.Plain)
        {
            Text = body
        };

        using (var client = new MailKit.Net.Smtp.SmtpClient())
        {
            client.Connect(_smtpSettings.SmtpServer, _smtpSettings.SmtpPort, _smtpSettings.EnableSsl);

            client.Send(message);
            client.Disconnect(true);
        }

        return Task.CompletedTask;
    }
}
