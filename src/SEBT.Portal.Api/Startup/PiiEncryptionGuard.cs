using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Api.Startup;

/// <summary>
/// Validates that PII encryption keys are not default or placeholder values in production.
/// </summary>
public static class PiiEncryptionGuard
{
    /// <summary>Matches <c>appsettings.json</c> sample ActiveKeyId — not safe for production.</summary>
    public const string ForbiddenDevelopmentActiveKeyId = "local-dev-primary";

    /// <summary>32× ASCII 'a' (256-bit) — sample key in repo; must not be used in production.</summary>
    public const string ForbiddenPlaceholderKeyMaterialBase64 = "YWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWFhYWE=";

    /// <summary>
    /// Validates configured PII encryption for production. Throws if missing, empty, or known placeholders.
    /// </summary>
    public static void ValidateForProduction(PiiEncryptionSettings? settings)
    {
        if (settings == null)
        {
            throw new InvalidOperationException(
                "PiiEncryption configuration section is missing. Configure PiiEncryption:ActiveKeyId and PiiEncryption:Keys.");
        }

        if (string.IsNullOrWhiteSpace(settings.ActiveKeyId))
        {
            throw new InvalidOperationException(
                "PiiEncryption:ActiveKeyId must be set in production (e.g. PIIENCRYPTION__ACTIVEKEYID).");
        }

        if (settings.Keys == null || settings.Keys.Count == 0)
        {
            throw new InvalidOperationException(
                "PiiEncryption:Keys must contain at least one key entry in production.");
        }

        if (string.Equals(settings.ActiveKeyId.Trim(), ForbiddenDevelopmentActiveKeyId, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"PiiEncryption:ActiveKeyId must not be '{ForbiddenDevelopmentActiveKeyId}' in production. " +
                "Use a deployment-specific key id and secrets management.");
        }

        foreach (var entry in settings.Keys)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.KeyMaterialBase64))
            {
                continue;
            }

            var material = entry.KeyMaterialBase64.Trim();
            if (string.Equals(material, ForbiddenPlaceholderKeyMaterialBase64, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "PiiEncryption key material must not use the repository placeholder Base64 value in production. " +
                    "Generate random 256-bit keys and store them in secrets (e.g. PIIENCRYPTION__KEYS__0__KEYMATERIALBASE64).");
            }
        }
    }
}
