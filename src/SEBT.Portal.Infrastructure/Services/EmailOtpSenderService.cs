using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Infrastructure.Services;

internal record OtpEmailTranslation(
    [property: JsonPropertyName("subject")] string Subject,
    [property: JsonPropertyName("programName")] string ProgramName,
    [property: JsonPropertyName("body1")] string Body1,
    [property: JsonPropertyName("body3")] string Body3);

/// <summary>
/// Sends OTP codes via HTML email, rendering the template in the recipient's language.
/// Translations are loaded from the EmailContent.{state}.json embedded resource,
/// generated from the content CSVs via <c>pnpm copy:generate</c>.
/// </summary>
public class EmailOtpSenderService(
    IOptionsMonitor<EmailOtpSenderServiceSettings> optionsMonitor,
    ILogger<EmailOtpSenderService> logger,
    ISmtpClientService smtpClientService) : IOtpSenderService
{
    private const string LogoContentId = "logo";
    private readonly EmailOtpSenderServiceSettings _settings = optionsMonitor.CurrentValue;
    private static readonly Lazy<string> _cachedTemplate = new(LoadEmailTemplate);
    private static readonly Lazy<byte[]> _cachedLogo = new(LoadLogoData);
    private readonly IReadOnlyDictionary<string, OtpEmailTranslation> _translations = LoadTranslations();

    public async Task<Result> SendOtpAsync(string to, string otp, string locale)
    {
        try
        {
            var translation = GetTranslation(locale);
            var htmlBody = RenderEmailTemplate(otp, locale, translation);
            var linkedResources = GetLinkedResources();

            await smtpClientService.SendEmailAsync(
                to,
                _settings.SenderEmail,
                translation.Subject,
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

    private OtpEmailTranslation GetTranslation(string locale)
    {
        if (_translations.TryGetValue(locale, out var translation))
            return translation;
        if (_translations.TryGetValue("en", out var fallback))
            return fallback;
        throw new InvalidOperationException(
            $"No OTP email translation found for locale '{locale}' and no 'en' fallback is present in the content file.");
    }

    private static string RenderEmailTemplate(string otp, string locale, OtpEmailTranslation translation)
    {
        var template = _cachedTemplate.Value;
        var logoHtml = $"<img src=\"cid:{LogoContentId}\" alt=\"{translation.ProgramName}\" width=\"140\" style=\"max-width: 100%; height: auto;\" />";

        return template
            .Replace("{{OtpCode}}", otp)
            .Replace("{{Locale}}", locale)
            .Replace("{{Subject}}", translation.Subject)
            .Replace("{{ProgramName}}", translation.ProgramName)
            .Replace("{{Body1}}", translation.Body1)
            .Replace("{{Body3}}", translation.Body3)
            .Replace("{{LogoHtml}}", logoHtml);
    }

    private static List<EmailLinkedResource> GetLinkedResources() =>
    [
        new EmailLinkedResource(LogoContentId, _cachedLogo.Value, "image/png", "logo.png")
    ];

    private static IReadOnlyDictionary<string, OtpEmailTranslation> LoadTranslations()
    {
        var state = Environment.GetEnvironmentVariable("STATE")?.ToLowerInvariant()
            ?? throw new InvalidOperationException("STATE environment variable is not set.");

        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"SEBT.Portal.Infrastructure.Templates.Email.EmailContent.{state}.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Email content resource not found for state '{state}'. " +
                $"Expected embedded resource: {resourceName}. " +
                $"Run 'pnpm copy:generate' and rebuild to regenerate.");

        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();

        return JsonSerializer.Deserialize<Dictionary<string, OtpEmailTranslation>>(json)
            ?? throw new InvalidOperationException($"Failed to deserialize email content for state '{state}'.");
    }

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
