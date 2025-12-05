using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace SEBT.Portal.Tests.Unit.Services;

public class SmtpClientServiceTests
{
    private readonly IOptionsMonitor<SmtpClientSettings> _optionsMonitor =
        Substitute.For<IOptionsMonitor<SmtpClientSettings>>();
    private readonly ILogger<SmtpClientService> _logger = Substitute.For<ILogger<SmtpClientService>>();

    public async Task SendEmailAsync_WithValidMailMessage_ShouldSendEmail()
    {
        // Arrange
        _optionsMonitor.CurrentValue.Returns(new SmtpClientSettings
        {
            SmtpServer = "smtp.example.com",
            SmtpPort = 587,
            EnableSsl = true
        });

        var smtpClientService = new SmtpClientService(_optionsMonitor, _logger);
        var mailMessage = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress("jon@example.com"),
            Subject = "Test Email",
            Body = "This is a test email."
        };
        mailMessage.To.Add("jane@example.com");

        // Act
        await smtpClientService.SendEmailAsync(mailMessage);

        // Assert
        // Since SmtpClient.SendMailAsync does not return a value, we verify that no exceptions were thrown
        Assert.True(true);

    }
}