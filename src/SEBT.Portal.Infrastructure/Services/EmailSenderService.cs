using SEBT.Portal.Core.Services;

public class EmailSenderService:IEmailSenderService
{
    public Task SendEmailAsync(string to, string subject, string body)
    {
        throw new NotImplementedException();
    }
}