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

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    [InlineData("am")]
    public async Task SendOtpAsync_WithDifferentLanguages_ShouldSetCorrectLangAttribute(string language)
    {
        // Arrange
        var emailSettings = new EmailOtpSenderServiceSettings
        {
            SenderEmail = "sender@example.com",
            SenderName = "Test Sender",
            Subject = "Test Subject",
            ProgramName = "Test Program",
            StateName = "Test State",
            ExpiryMinutes = 10,
            Language = language
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

        // Act
        await emailSenderService.SendOtpAsync("recipient@example.com", "123456");

        // Assert
        await _smtpClientService.Received().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains($"lang=\"{language}\"")),
            Arg.Any<IEnumerable<EmailLinkedResource>>());
    }

    [Theory]
    [InlineData(5)]
    [InlineData(10)]
    [InlineData(15)]
    [InlineData(30)]
    public async Task SendOtpAsync_WithDifferentExpiryMinutes_ShouldIncludeCorrectExpiry(int expiryMinutes)
    {
        // Arrange
        var emailSettings = new EmailOtpSenderServiceSettings
        {
            SenderEmail = "sender@example.com",
            SenderName = "Test Sender",
            Subject = "Test Subject",
            ProgramName = "Test Program",
            StateName = "Test State",
            ExpiryMinutes = expiryMinutes,
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

        // Act
        await emailSenderService.SendOtpAsync("recipient@example.com", "123456");

        // Assert
        await _smtpClientService.Received().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains(expiryMinutes.ToString())),
            Arg.Any<IEnumerable<EmailLinkedResource>>());
    }

    [Theory]
    [InlineData("DC SUN Bucks", "DC SUN Bucks")]
    [InlineData("CO Summer EBT", "Colorado Summer EBT")]
    [InlineData("VA SNAP", "Virginia SNAP Benefits")]
    public async Task SendOtpAsync_WithDifferentStateSettings_ShouldIncludeCorrectStateInfo(string programName, string stateName)
    {
        // Arrange
        var emailSettings = new EmailOtpSenderServiceSettings
        {
            SenderEmail = "sender@example.com",
            SenderName = "Test Sender",
            Subject = "Test Subject",
            ProgramName = programName,
            StateName = stateName,
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

        // Act
        await emailSenderService.SendOtpAsync("recipient@example.com", "123456");

        // Assert
        await _smtpClientService.Received().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(body =>
                body.Contains(programName) &&
                body.Contains(stateName)),
            Arg.Any<IEnumerable<EmailLinkedResource>>());
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("000000")]
    [InlineData("999999")]
    [InlineData("ABC123")]
    public async Task SendOtpAsync_WithDifferentOtpCodes_ShouldIncludeCorrectCode(string otpCode)
    {
        // Arrange
        var emailSettings = new EmailOtpSenderServiceSettings
        {
            SenderEmail = "sender@example.com",
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

        // Act
        await emailSenderService.SendOtpAsync("recipient@example.com", otpCode);

        // Assert
        await _smtpClientService.Received().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains(otpCode)),
            Arg.Any<IEnumerable<EmailLinkedResource>>());
    }

    [Fact]
    public async Task SendOtpAsync_ShouldIncludeLogoAsLinkedResource()
    {
        // Arrange
        var emailSettings = new EmailOtpSenderServiceSettings
        {
            SenderEmail = "sender@example.com",
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

        // Act
        await emailSenderService.SendOtpAsync("recipient@example.com", "123456");

        // Assert - verify logo linked resource is included with correct properties
        await _smtpClientService.Received().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains("cid:logo")),
            Arg.Is<IEnumerable<EmailLinkedResource>>(resources =>
                resources.Count() == 1 &&
                resources.First().ContentId == "logo" &&
                resources.First().ContentType == "image/png" &&
                resources.First().FileName == "logo.png" &&
                resources.First().Data.Length > 0));
    }

    [Fact]
    public async Task SendOtpAsync_WhenSmtpServiceThrows_ShouldReturnPreconditionFailedResult()
    {
        // Arrange
        var emailSettings = new EmailOtpSenderServiceSettings
        {
            SenderEmail = "sender@example.com",
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
            .Returns(Task.FromException(new Exception("SMTP connection failed")));

        var emailSenderService = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);

        // Act
        var result = await emailSenderService.SendOtpAsync("recipient@example.com", "123456");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.IsType<PreconditionFailedResult>(result);
    }

    [Fact]
    public async Task SendOtpAsync_ShouldGenerateCorrectLogoAltText()
    {
        // Arrange
        var emailSettings = new EmailOtpSenderServiceSettings
        {
            SenderEmail = "sender@example.com",
            SenderName = "Test Sender",
            Subject = "Test Subject",
            ProgramName = "My Custom Program",
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

        // Act
        await emailSenderService.SendOtpAsync("recipient@example.com", "123456");

        // Assert - verify logo alt text uses ProgramName
        await _smtpClientService.Received().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains($"alt=\"{emailSettings.ProgramName}\"")),
            Arg.Any<IEnumerable<EmailLinkedResource>>());
    }
}
