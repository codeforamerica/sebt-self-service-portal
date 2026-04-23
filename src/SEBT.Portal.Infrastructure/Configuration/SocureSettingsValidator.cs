using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Validates SocureSettings at startup.
/// When UseStub is false (real Socure integration), ApiKey and WebhookSecret are required.
/// UseStub is only permitted in Development to prevent accidental bypass in deployed environments.
/// </summary>
public class SocureSettingsValidator(IHostEnvironment environment) : IValidateOptions<SocureSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SocureSettings options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("Socure configuration section is not present.");
        }

        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (options.ChallengeExpirationMinutes < 1 || options.ChallengeExpirationMinutes > 1440)
        {
            return ValidateOptionsResult.Fail(
                "Socure:ChallengeExpirationMinutes must be between 1 and 1440.");
        }

        if (options.DocvTransactionTokenTtlMinutes < 1 || options.DocvTransactionTokenTtlMinutes > 120)
        {
            return ValidateOptionsResult.Fail(
                "Socure:DocvTransactionTokenTtlMinutes must be between 1 and 120.");
        }

        // UseStub bypasses webhook signature validation — only safe in Development
        if (options.UseStub && !environment.IsDevelopment())
        {
            return ValidateOptionsResult.Fail(
                "Socure:UseStub cannot be enabled outside of Development. " +
                "It bypasses webhook signature validation on an anonymous endpoint.");
        }

        // When using the real client, API key and webhook secret are required
        if (!options.UseStub)
        {
            if (string.IsNullOrWhiteSpace(options.ApiKey))
            {
                return ValidateOptionsResult.Fail(
                    "Socure:ApiKey is required when UseStub is false.");
            }

            if (string.IsNullOrWhiteSpace(options.WebhookSecret))
            {
                return ValidateOptionsResult.Fail(
                    "Socure:WebhookSecret is required when UseStub is false.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
