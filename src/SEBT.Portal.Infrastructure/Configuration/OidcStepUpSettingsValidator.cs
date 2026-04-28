using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// When any step-up value is set, requires a complete step-up client (and a redirect via step-up or primary callback URI).
/// </summary>
public class OidcStepUpSettingsValidator(IConfiguration configuration) : IValidateOptions<OidcStepUpSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OidcStepUpSettings options)
    {
        if (options == null)
            return ValidateOptionsResult.Fail("Oidc step-up configuration is null.");

        static bool HasValue(string? s) => !string.IsNullOrWhiteSpace(s);

        var anyStepUpKey = HasValue(options.DiscoveryEndpoint) || HasValue(options.ClientId)
            || HasValue(options.RedirectUri);
        if (!anyStepUpKey)
            return ValidateOptionsResult.Success;

        var callbackRedirect = configuration["Oidc:CallbackRedirectUri"];
        var missing = new List<string>();
        if (!HasValue(options.DiscoveryEndpoint))
            missing.Add("Oidc:StepUp:DiscoveryEndpoint");
        if (!HasValue(options.ClientId))
            missing.Add("Oidc:StepUp:ClientId");
        if (!HasValue(options.RedirectUri) && !HasValue(callbackRedirect))
            missing.Add("Oidc:StepUp:RedirectUri (or Oidc:CallbackRedirectUri as fallback)");

        if (missing.Count == 0)
            return ValidateOptionsResult.Success;

        return ValidateOptionsResult.Fail(
            "OIDC step-up is partially configured. Either remove all Oidc:StepUp values or supply a complete step-up client. " +
            "Missing: " + string.Join(", ", missing));
    }
}
