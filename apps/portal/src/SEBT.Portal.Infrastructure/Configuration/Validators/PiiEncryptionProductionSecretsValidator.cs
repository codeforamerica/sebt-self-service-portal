using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration.Validators;

public class PiiEncryptionProductionSecretsValidator(IHostEnvironment environment)
    : IValidateOptions<PiiEncryptionSettings>
{
    /// <summary>Matches <c>appsettings.json</c> sample ActiveKeyId — not safe for production.</summary>
    public const string ForbiddenDevelopmentActiveKeyId = "local-dev-primary";

    /// <summary>32× ASCII 'a' (256-bit) — sample key in repo; must not be used in production.</summary>
    public const string ForbiddenPlaceholderKeyMaterialBase64 = "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWE=";

    public ValidateOptionsResult Validate(string? name, PiiEncryptionSettings options)
    {
        if (!environment.IsProduction())
        {
            return ValidateOptionsResult.Skip;
        }

        if (!options.EncryptAtRest)
        {
            return ValidateOptionsResult.Skip;
        }

        if (string.Equals(options.ActiveKeyId.Trim(), ForbiddenDevelopmentActiveKeyId,
                StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                $"PiiEncryption:ActiveKeyId must not be '{ForbiddenDevelopmentActiveKeyId}' in production. " +
                "Use a deployment-specific key id and secrets management.");
        }

        foreach (var entry in options.Keys)
        {
            var material = entry.KeyMaterialBase64.Trim();

            if (string.Equals(material, ForbiddenPlaceholderKeyMaterialBase64, StringComparison.OrdinalIgnoreCase))
            {
                return ValidateOptionsResult.Fail(
                    "PiiEncryption key material must not use the repository placeholder Base64 value in production. " +
                    "Generate random 256-bit keys and store them in secrets (e.g. PIIENCRYPTION__KEYS__0__KEYMATERIALBASE64).");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
