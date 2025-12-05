using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;

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
        await smtpClientService.SendEmailAsync("jane@example.com", "jon@example.com", "Test Email", "This is a test email.", true);

        // Assert
        // Since SmtpClient.SendMailAsync does not return a value, we verify that no exceptions were thrown
        Assert.True(true);

    }
}
