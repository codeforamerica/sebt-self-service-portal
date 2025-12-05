using System.Net.Mail;

namespace SEBT.Portal.Core.Services
{
    public interface ISmtpClientService
    {
        Task SendEmailAsync(MailMessage message);
    }
}