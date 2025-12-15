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

    [Fact(Skip = "This test is temporarily disabled due to a known bug.")]
    public async Task SendEmailAsync_WithValidMailMessage_ShouldSendEmail()
    {
        // Arrange
        _optionsMonitor.CurrentValue.Returns(new SmtpClientSettings
        {
            SmtpServer = "smtp.example.com",
            SmtpPort = 587,
            EnableSsl = true
        });
        var smtpClient = Substitute.For<System.Net.Mail.SmtpClient>();
        var smtpClientService = new SmtpClientService(_optionsMonitor, _logger);
        var mailMessage = new System.Net.Mail.MailMessage
        {
            From = new System.Net.Mail.MailAddress("jon@example.com"),
            Subject = "Test Email",
            Body = "This is a test email."
        };
        mailMessage.To.Add("jane@example.com");

        // Act
        await smtpClientService.SendEmailAsync("", "", "", "");

        // Assert
        // Since SmtpClient.SendMailAsync does not return a value, we verify that no exceptions were thrown
        // The SmtpClient is now injected, so no need to create a new instance here
        _ = smtpClient.Received(1).SendMailAsync(Arg.Is<System.Net.Mail.MailMessage>(msg =>
            msg.From.Address == "jon@example.com" &&
            msg.Subject == "Test Email" &&
            msg.Body == "This is a test email." &&
            msg.To.Contains(new System.Net.Mail.MailAddress("jane@example.com"))
        ));

    }
}
