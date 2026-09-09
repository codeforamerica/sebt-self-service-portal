using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration.Validators;

public class OidcSettingsValidator : IValidateOptions<OidcSettings>
{
    public ValidateOptionsResult Validate(string? name, OidcSettings options)
    {
        var key = options.CompleteLoginSigningKey;

        if (!string.IsNullOrEmpty(key) && key.Length < 32)
        {
            return ValidateOptionsResult.Fail(
                $"Oidc:CompleteLoginSigningKey must be at least 32 characters (got {key.Length}). " +
                "HMAC-SHA256 requires a 256-bit key for full security.");
        }

        return ValidateOptionsResult.Success;
    }
}
