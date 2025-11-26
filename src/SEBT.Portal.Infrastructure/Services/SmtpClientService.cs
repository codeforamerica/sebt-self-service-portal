using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public class SmtpClientService(IOptionsMonitor<SmtpClientSettings> optionsMonitor, ILogger<SmtpClientService> logger)
    : ISmtpClientService
{
    private readonly SmtpClientSettings smtpClientSettings = optionsMonitor.CurrentValue;

    public async Task SendEmailAsync(MailMessage message)
    {
        // Configure the SMTP client
        SmtpClient smtpClient = new SmtpClient(smtpClientSettings.SmtpServer, smtpClientSettings.SmtpPort);
        smtpClient.EnableSsl = true;

        // Send the email        
        try
        {
            await smtpClient.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Error sending email to: {message.To}");
        }
    }
}