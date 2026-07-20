using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Validates cross-field invariants on <see cref="JwtSettings"/> that data annotations
/// cannot express. Range-only checks live on the settings type itself.
/// </summary>
public class JwtSettingsValidator : IValidateOptions<JwtSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, JwtSettings options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("JwtSettings configuration section is not present.");
        }

        // An idle window longer than the absolute cap is nonsensical: the absolute cap
        // would never be reached in normal use because the idle timeout would always
        // have already revoked the session.
        if (options.AbsoluteExpirationMinutes < options.ExpirationMinutes)
        {
            return ValidateOptionsResult.Fail(
                "JwtSettings:AbsoluteExpirationMinutes must be greater than or equal to ExpirationMinutes.");
        }

        return ValidateOptionsResult.Success;
    }
}
