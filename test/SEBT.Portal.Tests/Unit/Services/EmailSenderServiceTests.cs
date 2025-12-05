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
            Subject = "Test Subject",
            HtmlPreOtp = "<h1>Your OTP is:</h1><p>",
            HtmlPostOtp = "</p><p>Please use it wisely.</p>"
        };
        _optionsMonitor.CurrentValue.Returns(emailSettings);

        _smtpClientService.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
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
            Subject = "Test Subject",
            HtmlPreOtp = "<h1>Your OTP is:</h1><p>",
            HtmlPostOtp = "</p><p>Please use it wisely.</p>"
        };
        _optionsMonitor.CurrentValue.Returns(emailSettings);
        _smtpClientService.SendEmailAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>())
            .Returns(Task.CompletedTask);

        var emailSenderService = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);
        var sendEmailResult = await emailSenderService.SendOtpAsync("jane@example.com", "123456");

        // Assert
        await _smtpClientService.Received().SendEmailAsync(
                Arg.Any<string>(),
                emailSettings.SenderEmail,
                emailSettings.Subject,
                $"{emailSettings.HtmlPreOtp}{"123456"}{emailSettings.HtmlPostOtp}",
                true);
    }
}
