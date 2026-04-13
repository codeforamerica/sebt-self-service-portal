using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Validates MinimumIalSettings at startup.
/// All three properties are required — there is no sensible state-agnostic default.
/// Each state must configure these in its appsettings overlay.
/// </summary>
public class MinimumIalSettingsValidator : IValidateOptions<MinimumIalSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, MinimumIalSettings options)
    {
        if (options.ApplicationCases is null)
        {
            return ValidateOptionsResult.Fail(
                "MinimumIal:ApplicationCases is required. Configure it in your state's appsettings overlay.");
        }

        if (options.CoLoadedStreamlineCases is null)
        {
            return ValidateOptionsResult.Fail(
                "MinimumIal:CoLoadedStreamlineCases is required. Configure it in your state's appsettings overlay.");
        }

        if (options.NonCoLoadedStreamlineCases is null)
        {
            return ValidateOptionsResult.Fail(
                "MinimumIal:NonCoLoadedStreamlineCases is required. Configure it in your state's appsettings overlay.");
        }

        return ValidateOptionsResult.Success;
    }
}
