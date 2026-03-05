using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Validates SocureSettings at startup.
/// When UseStub is false (real Socure integration), ApiKey and WebhookSecret are required (D11).
/// </summary>
public class SocureSettingsValidator : IValidateOptions<SocureSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SocureSettings options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("Socure configuration section is not present.");
        }

        if (options.ChallengeExpirationMinutes < 1 || options.ChallengeExpirationMinutes > 1440)
        {
            return ValidateOptionsResult.Fail(
                "Socure:ChallengeExpirationMinutes must be between 1 and 1440.");
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
                    "Socure:WebhookSecret is required when UseStub is false (D11).");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
