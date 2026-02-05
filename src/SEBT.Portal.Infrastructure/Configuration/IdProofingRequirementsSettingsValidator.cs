using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Validates IdProofingRequirementsSettings at startup.
/// </summary>
public class IdProofingRequirementsSettingsValidator : IValidateOptions<IdProofingRequirementsSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, IdProofingRequirementsSettings options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("IdProofingRequirements configuration is null.");
        }

        return ValidateOptionsResult.Success;
    }
}
