using System.Net.Mail;

public interface ISmtpClientService
{
    Task SendEmailAsync(MailMessage message);
}