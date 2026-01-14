using Microsoft.Extensions.Options;
using MimeKit;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

public class MailKitClientService(IOptionsMonitor<SmtpClientSettings> optionsMonitor) : ISmtpClientService
{
    private readonly SmtpClientSettings _smtpSettings = optionsMonitor.CurrentValue;

    public Task SendEmailAsync(string to, string from, string subject, string body)
    {
        return SendEmailAsync(to, from, subject, body, []);
    }

    public async Task SendEmailAsync(string to, string from, string subject, string body, IEnumerable<EmailLinkedResource> linkedResources)
    {
        using var message = new MimeMessage();
        message.From.Add(new MailboxAddress(from, from));
        message.To.Add(new MailboxAddress(to, to));
        message.Subject = subject;

        var builder = new BodyBuilder { HtmlBody = body };

        foreach (var resource in linkedResources)
        {
            var linkedResource = builder.LinkedResources.Add(resource.FileName, resource.Data, ContentType.Parse(resource.ContentType));
            linkedResource.ContentId = resource.ContentId;
            linkedResource.ContentDisposition = new ContentDisposition(ContentDisposition.Inline);
        }

        message.Body = builder.ToMessageBody();

        using var client = new MailKit.Net.Smtp.SmtpClient();
        try
        {
            await client.ConnectAsync(_smtpSettings.SmtpServer, _smtpSettings.SmtpPort, _smtpSettings.EnableSsl);
            await client.SendAsync(message);
        }
        finally
        {
            await client.DisconnectAsync(true);
        }
    }
}
