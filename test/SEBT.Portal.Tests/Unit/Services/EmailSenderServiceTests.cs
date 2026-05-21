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

    public EmailSenderServiceTests()
    {
        Environment.SetEnvironmentVariable("STATE", "dc");
        _optionsMonitor.CurrentValue.Returns(new EmailOtpSenderServiceSettings { SenderEmail = "sender@example.com" });
        _smtpClientService.SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<EmailLinkedResource>>())
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task SendOtpAsync_WithValidParams_ShouldSendEmailSuccessfully()
    {
        var service = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);

        var result = await service.SendOtpAsync("jane@example.com", "123456", "en");

        Assert.True(result.IsSuccess);
        Assert.IsType<SuccessResult>(result);
    }

    [Fact]
    public async Task SendOtpAsync_WithEnglishLocale_ShouldUseCorrectSubjectAndBody()
    {
        var service = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);

        await service.SendOtpAsync("jane@example.com", "123456", "en");

        await _smtpClientService.Received().SendEmailAsync(
            "jane@example.com",
            "sender@example.com",
            "Your DC SUN Bucks Login Code",
            Arg.Is<string>(body =>
                body.Contains("123456") &&
                body.Contains("DC SUN Bucks") &&
                body.Contains("Use this code to log in to your account.") &&
                body.Contains("lang=\"en\"") &&
                body.Contains("cid:logo")),
            Arg.Is<IEnumerable<EmailLinkedResource>>(resources =>
                resources.Any(r => r.ContentId == "logo" && r.ContentType == "image/png")));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    [InlineData("am")]
    public async Task SendOtpAsync_WithSupportedLocale_ShouldSetCorrectLangAttribute(string locale)
    {
        var service = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);

        await service.SendOtpAsync("recipient@example.com", "123456", locale);

        await _smtpClientService.Received().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains($"lang=\"{locale}\"")),
            Arg.Any<IEnumerable<EmailLinkedResource>>());
    }

    [Fact]
    public async Task SendOtpAsync_WithUnknownLocale_ShouldFallBackToEnglishContent()
    {
        var service = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);

        await service.SendOtpAsync("recipient@example.com", "123456", "fr");

        await _smtpClientService.Received().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            "Your DC SUN Bucks Login Code",
            Arg.Is<string>(body => body.Contains("Use this code to log in to your account.")),
            Arg.Any<IEnumerable<EmailLinkedResource>>());
    }

    [Fact]
    public async Task SendOtpAsync_WithUnknownLocale_ShouldUseFallbackLocaleInLangAttribute()
    {
        var service = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);

        await service.SendOtpAsync("recipient@example.com", "123456", "fr");

        await _smtpClientService.Received().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains("lang=\"en\"") && !body.Contains("lang=\"fr\"")),
            Arg.Any<IEnumerable<EmailLinkedResource>>());
    }

    [Fact]
    public async Task SendOtpAsync_WithInjectedLocale_ShouldNotRenderRawLocaleInHtml()
    {
        const string injectedLocale = "\"><script>alert(1)</script><html lang=\"";
        var service = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);

        await service.SendOtpAsync("recipient@example.com", "123456", injectedLocale);

        await _smtpClientService.Received().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(body => !body.Contains(injectedLocale) && body.Contains("lang=\"en\"")),
            Arg.Any<IEnumerable<EmailLinkedResource>>());
    }

    [Theory]
    [InlineData("es", "Tu código de acceso de DC SUN Bucks", "Usa este código para iniciar sesión en tu cuenta")]
    [InlineData("am", "የእርስዎ የDC SUN Bucks የመግቢያ ኮድ", "ወደ መለያዎ ለመግባት ይህንን ኮድ ይጠቀሙ።")]
    public async Task SendOtpAsync_WithNonEnglishLocale_ShouldUseLocalizedContent(string locale, string expectedSubject, string expectedBody1)
    {
        var service = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);

        await service.SendOtpAsync("recipient@example.com", "123456", locale);

        await _smtpClientService.Received().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            expectedSubject,
            Arg.Is<string>(body => body.Contains(expectedBody1)),
            Arg.Any<IEnumerable<EmailLinkedResource>>());
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("000000")]
    [InlineData("999999")]
    [InlineData("ABC123")]
    public async Task SendOtpAsync_WithDifferentOtpCodes_ShouldIncludeCorrectCode(string otpCode)
    {
        var service = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);

        await service.SendOtpAsync("recipient@example.com", otpCode, "en");

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
        var service = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);

        await service.SendOtpAsync("recipient@example.com", "123456", "en");

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
        _smtpClientService.SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<IEnumerable<EmailLinkedResource>>())
            .Returns(Task.FromException(new Exception("SMTP connection failed")));

        var service = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);

        var result = await service.SendOtpAsync("recipient@example.com", "123456", "en");

        Assert.False(result.IsSuccess);
        Assert.IsType<PreconditionFailedResult>(result);
    }

    [Fact]
    public async Task SendOtpAsync_ShouldUseTranslatedProgramNameAsLogoAltText()
    {
        var service = new EmailOtpSenderService(_optionsMonitor, _logger, _smtpClientService);

        await service.SendOtpAsync("recipient@example.com", "123456", "en");

        await _smtpClientService.Received().SendEmailAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<string>(body => body.Contains("alt=\"DC SUN Bucks\"")),
            Arg.Any<IEnumerable<EmailLinkedResource>>());
    }
}
