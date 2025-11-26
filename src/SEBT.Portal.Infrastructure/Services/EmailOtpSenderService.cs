using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

public class EmailOtpSenderService(
    IOptionsMonitor<EmailOtpSenderServiceSettings> optionsMonitor,
    ILogger<EmailOtpSenderService> logger,
    ISmtpClientService smtpClientService) : IOtpSenderService
{
    private readonly EmailOtpSenderServiceSettings settings = optionsMonitor.CurrentValue;

    public async Task<Result> SendOtpAsync(string to, string otp)
    {
        // Create the email message
        MailMessage message = new MailMessage();
        message.From = new MailAddress(settings.SenderEmail);
        message.To.Add(to);
        message.Subject = settings.Subject;
        message.IsBodyHtml = true;
        message.Body = $"{settings.HtmlPreOtp}{otp}{settings.HtmlPostOtp}";

        try
        {
            // Send the email        
            await smtpClientService.SendEmailAsync(message);
            logger.LogInformation($"OTP email sent to {to}.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, $"Failed to send OTP to: {to}");
        }

        return new SuccessResult();
    }
}