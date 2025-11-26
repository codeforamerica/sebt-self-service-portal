using Castle.Core.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.Core.AppSettings;
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
        _optionsMonitor.CurrentValue.Returns(new EmailOtpSenderServiceSettings
        {
            SenderEmail = "jon@example.com",
            Subject = "Test Subject",
            HtmlPreOtp = "<h1>Your OTP is:</h1><p>",
            HtmlPostOtp = "</p><p>Please use it wisely.</p>"
        });

        _smtpClientService.SendEmailAsync(Arg.Any<System.Net.Mail.MailMessage>())
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
        _optionsMonitor.CurrentValue.Returns(new EmailOtpSenderServiceSettings
        {
            SenderEmail = "jon@example.com",
            Subject = "Test Subject",
            HtmlPreOtp = "<h1>Your OTP is:</h1><p>",
            HtmlPostOtp = "</p><p>Please use it wisely.</p>"
        });

        _smtpClientService.SendEmailAsync(Arg.Any<System.Net.Mail.MailMessage>())
            .Returns(Task.CompletedTask);

        var emailSenderService = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);
        var sendEmailResult = await emailSenderService.SendOtpAsync("jane@example.com", "123456");

        // Assert
        await _smtpClientService.Received(1)
            .SendEmailAsync(Arg.Is<System.Net.Mail.MailMessage>(msg =>
                msg.From.Address == _optionsMonitor.CurrentValue.SenderEmail &&
                msg.To.Contains(new System.Net.Mail.MailAddress("jane@example.com")) &&
                msg.Subject == _optionsMonitor.CurrentValue.Subject &&
                msg.Body == $"{_optionsMonitor.CurrentValue.HtmlPreOtp}123456{_optionsMonitor.CurrentValue.HtmlPostOtp}"
            ));
    }
}