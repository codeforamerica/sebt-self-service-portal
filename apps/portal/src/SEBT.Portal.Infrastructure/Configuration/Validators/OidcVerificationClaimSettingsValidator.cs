using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration.Validators;

/// <summary>
/// When OIDC is enabled (<c>Oidc:DiscoveryEndpoint</c> is set), requires the verification level and
/// date claim names. Without them every OIDC user would read as unverified.
/// </summary>
public class OidcVerificationClaimSettingsValidator(IConfiguration configuration)
    : IValidateOptions<OidcVerificationClaimSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OidcVerificationClaimSettings options)
    {
        // Bound from IConfiguration rather than IOptions<OidcSettings> so validating one options type
        // cannot pull in another.
        var discoveryEndpoint = configuration
            .GetSection(OidcSettings.SectionName).Get<OidcSettings>()?.DiscoveryEndpoint;
        if (string.IsNullOrWhiteSpace(discoveryEndpoint))
        {
            return ValidateOptionsResult.Success;
        }

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(options.LevelClaimName))
        {
            missing.Add($"{OidcVerificationClaimSettings.SectionName}:{nameof(options.LevelClaimName)}");
        }

        if (string.IsNullOrWhiteSpace(options.DateClaimName))
        {
            missing.Add($"{OidcVerificationClaimSettings.SectionName}:{nameof(options.DateClaimName)}");
        }

        if (missing.Count == 0)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            "OIDC is enabled (Oidc:DiscoveryEndpoint is set) but verification claim names are missing: "
            + string.Join(", ", missing));
    }
}
