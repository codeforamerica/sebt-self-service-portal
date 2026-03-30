using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// When Smarty is enabled, embedded keys are required at startup.
/// </summary>
public sealed class SmartySettingsValidator : IValidateOptions<SmartySettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, SmartySettings options)
    {
        if (!options.Enabled)
        {
            return ValidateOptionsResult.Success;
        }

        if (string.IsNullOrWhiteSpace(options.AuthId))
        {
            return ValidateOptionsResult.Fail("Smarty:AuthId is required when Smarty:Enabled is true.");
        }

        if (string.IsNullOrWhiteSpace(options.AuthToken))
        {
            return ValidateOptionsResult.Fail("Smarty:AuthToken is required when Smarty:Enabled is true.");
        }

        if (options.TimeoutSeconds < 1 || options.TimeoutSeconds > 120)
        {
            return ValidateOptionsResult.Fail("Smarty:TimeoutSeconds must be between 1 and 120.");
        }

        return ValidateOptionsResult.Success;
    }
}
