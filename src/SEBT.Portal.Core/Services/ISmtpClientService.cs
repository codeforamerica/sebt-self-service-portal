using System.Net.Mail;

namespace SEBT.Portal.Core.Services
{
    public interface ISmtpClientService
    {
        Task SendEmailAsync(string to, string from, string subject, string body, bool isBodyHtml = true);
    }
}
