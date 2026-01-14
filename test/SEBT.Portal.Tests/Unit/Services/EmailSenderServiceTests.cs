using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Infrastructure.Services;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Tests.Unit.Services;

public class EmailSenderServiceTests
{
    private readonly IOptionsMonitor<EmailOtpSenderServiceSettings> _optionsMonitor =
        Substitute.For<IOptionsMonitor<EmailOtpSenderServiceSettings>>();
    private readonly ILogger<EmailOtpSenderService> _logger = Substitute.For<ILogger<EmailOtpSenderService>>();
    private readonly ISmtpClientService _smtpClientService = Substitute.For<ISmtpClientService>();

    [Fact]
    public async Task SendOtpAsync_WithValidParams_ShouldSendEmailSuccessfully()
    {
        // Arrange
        var emailSettings = new EmailOtpSenderServiceSettings
        {
            SenderEmail = "jon@example.com",
            SenderName = "Test Sender",
            Subject = "Test Subject",
            ProgramName = "Test Program",
            StateName = "Test State",
            ExpiryMinutes = 10,
            Language = "en"
        };
        _optionsMonitor.CurrentValue.Returns(emailSettings);

        _smtpClientService.SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<EmailLinkedResource>>())
            .Returns(Task.CompletedTask);

        var emailSenderService = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);
        var sendEmailResult = await emailSenderService.SendOtpAsync("jane@example.com", "123456");

        // Assert
        Assert.True(sendEmailResult.IsSuccess);
        Assert.IsType<SuccessResult>(sendEmailResult);
    }

    [Fact]
    public async Task SendOtpAsync_WithValidParams_ShouldUseSettingsCorrectly()
    {
        // Arrange
        var emailSettings = new EmailOtpSenderServiceSettings
        {
            SenderEmail = "jon@example.com",
            SenderName = "Test Sender",
            Subject = "Test Subject",
            ProgramName = "Test Program",
            StateName = "Test State",
            ExpiryMinutes = 10,
            Language = "es"
        };
        _optionsMonitor.CurrentValue.Returns(emailSettings);
        _smtpClientService.SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<EmailLinkedResource>>())
            .Returns(Task.CompletedTask);

        var emailSenderService = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);
        await emailSenderService.SendOtpAsync("jane@example.com", "123456");

        // Assert - verify email was sent with correct sender, subject, HTML body containing OTP and settings, and linked resources
        await _smtpClientService.Received().SendEmailAsync(
            "jane@example.com",
            emailSettings.SenderEmail,
            emailSettings.Subject,
            Arg.Is<string>(body =>
                body.Contains("123456") &&
                body.Contains(emailSettings.StateName) &&
                body.Contains(emailSettings.ProgramName) &&
                body.Contains(emailSettings.ExpiryMinutes.ToString()) &&
                body.Contains($"lang=\"{emailSettings.Language}\"") &&
                body.Contains("cid:logo")),
            Arg.Is<IEnumerable<EmailLinkedResource>>(resources =>
                resources.Any(r => r.ContentId == "logo" && r.ContentType == "image/png")));
    }
}
