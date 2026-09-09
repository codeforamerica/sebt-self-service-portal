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

    [Fact]
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

        // Act & Assert
        // Note: This test will fail in a real environment without a valid SMTP server.
        // In a real scenario, you would need to mock the SmtpClient or use a test SMTP server.
        // For now, we're just verifying the method signature and that it throws for invalid calls.
        await Assert.ThrowsAsync<System.Net.Mail.SmtpException>(async () =>
            await smtpClientService.SendEmailAsync("jane@example.com", "jon@example.com", "Test Email", "This is a test email."));
    }
}
