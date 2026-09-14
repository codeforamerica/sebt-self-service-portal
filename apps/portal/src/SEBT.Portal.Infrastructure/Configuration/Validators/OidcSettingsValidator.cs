using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration.Validators;

/// <summary>
/// When a deployment configures an OIDC tenant, requires the rest of the client to be
/// present and usable.
///
/// A discovery endpoint is what marks the instance as an OIDC tenant — the state allowlist
/// reads it the same way — so an absent one means OIDC is off and nothing else is required.
/// Once it is set, a gap in the remaining values does not surface until a visitor is already
/// mid-login: the authorize redirect succeeds, and the failure lands after they return from
/// the IdP. Failing the boot instead keeps that from reaching a household.
/// </summary>
public class OidcSettingsValidator : IValidateOptions<OidcSettings>
{
    private const int MinimumSigningKeyLength = 32;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OidcSettings options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("Oidc configuration is null.");
        }

        static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);

        if (!HasValue(options.DiscoveryEndpoint))
        {
            return ValidateOptionsResult.Success;
        }

        // Collected rather than returned one at a time so a single boot tells an operator
        // everything to fix.
        var problems = new List<string>();

        if (!HasValue(options.ClientId))
        {
            problems.Add("Oidc:ClientId is required");
        }

        if (!HasValue(options.CallbackRedirectUri))
        {
            problems.Add("Oidc:CallbackRedirectUri is required");
        }

        var signingKey = options.CompleteLoginSigningKey;
        if (!HasValue(signingKey))
        {
            problems.Add("Oidc:CompleteLoginSigningKey is required");
        }
        else if (signingKey!.Length < MinimumSigningKeyLength)
        {
            problems.Add(
                $"Oidc:CompleteLoginSigningKey must be at least {MinimumSigningKeyLength} characters " +
                $"(got {signingKey.Length}); HMAC-SHA256 requires a 256-bit key for full security");
        }

        if (problems.Count == 0)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            "OIDC is enabled (Oidc:DiscoveryEndpoint is set) but the client is incomplete. " +
            "Either remove Oidc:DiscoveryEndpoint to disable OIDC, or supply the missing values. " +
            "Problems: " + string.Join("; ", problems));
    }
}
