using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Service responsible for sending One-Time Password (OTP) codes via email.
/// </summary>
/// <remarks>
/// This service uses SMTP to send HTML-formatted emails containing OTP codes
/// for user authentication purposes. Email content is loaded from an embedded
/// HTML template and populated with configurable values.
/// </remarks>
/// <param name="optionsMonitor">The options monitor for email sender settings.</param>
/// <param name="smtpClientService">The SMTP client service used to send emails.</param>
/// <param name="logger">The logger for recording email sending operations.</param>
public class EmailOtpSenderService(
    IOptionsMonitor<EmailOtpSenderServiceSettings> optionsMonitor,
    ILogger<EmailOtpSenderService> logger,
    ISmtpClientService smtpClientService) : IOtpSenderService
{
    private const string LogoContentId = "logo";
    private readonly EmailOtpSenderServiceSettings _settings = optionsMonitor.CurrentValue;
    private static readonly Lazy<string> _cachedTemplate = new(LoadEmailTemplate);
    private static readonly Lazy<byte[]> _cachedLogo = new(LoadLogoData);

    public async Task<Result> SendOtpAsync(string to, string otp)
    {
        try
        {
            var htmlBody = RenderEmailTemplate(otp);
            var linkedResources = GetLinkedResources();

            await smtpClientService.SendEmailAsync(
                to,
                _settings.SenderEmail,
                _settings.Subject,
                htmlBody,
                linkedResources);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send OTP.");
            return new PreconditionFailedResult(PreconditionFailedReason.Conflict, "Failed to send OTP email.");
        }

        return new SuccessResult();
    }

    /// <summary>
    /// Renders the OTP email template with the provided OTP code and configured settings.
    /// </summary>
    /// <param name="otp">The one-time password code to include in the email.</param>
    /// <returns>The fully rendered HTML email content.</returns>
    private string RenderEmailTemplate(string otp)
    {
        var template = _cachedTemplate.Value;
        var logoHtml = $"<img src=\"cid:{LogoContentId}\" alt=\"{_settings.ProgramName}\" width=\"140\" style=\"max-width: 100%; height: auto;\" />";

        return template
            .Replace("{{OtpCode}}", otp)
            .Replace("{{StateName}}", _settings.StateName)
            .Replace("{{ProgramName}}", _settings.ProgramName)
            .Replace("{{ExpiryMinutes}}", _settings.ExpiryMinutes.ToString())
            .Replace("{{Language}}", _settings.Language)
            .Replace("{{LogoHtml}}", logoHtml);
    }

    /// <summary>
    /// Gets the linked resources (embedded images) for the email.
    /// </summary>
    /// <returns>Collection of linked resources to embed in the email.</returns>
    private static List<EmailLinkedResource> GetLinkedResources()
    {
        return
        [
            new EmailLinkedResource(LogoContentId, _cachedLogo.Value, "image/png", "logo.png")
        ];
    }

    /// <summary>
    /// Loads the logo image data from the embedded resource.
    /// </summary>
    /// <returns>The logo image as a byte array.</returns>
    private static byte[] LoadLogoData()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "SEBT.Portal.Infrastructure.Templates.Email.logo.png";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Logo image not found: {resourceName}");

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        return memoryStream.ToArray();
    }

    /// <summary>
    /// Loads the email template from the embedded resource.
    /// </summary>
    /// <returns>The raw HTML template string.</returns>
    private static string LoadEmailTemplate()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "SEBT.Portal.Infrastructure.Templates.Email.OtpEmail.html";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Email template not found: {resourceName}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
