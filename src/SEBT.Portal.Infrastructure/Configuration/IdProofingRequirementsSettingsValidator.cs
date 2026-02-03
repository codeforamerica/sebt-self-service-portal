using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Validates IdProofingRequirementsSettings at startup.
/// Valid values for each PII type are "None" or "IAL1" (case-insensitive).
/// </summary>
public class IdProofingRequirementsSettingsValidator : IValidateOptions<IdProofingRequirementsSettings>
{
    private static readonly string[] ValidValues = ["None", "IAL1"];

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, IdProofingRequirementsSettings options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("IdProofingRequirements configuration is null.");
        }

        var failures = new List<string>();

        ValidateValue(options.Address, nameof(IdProofingRequirementsSettings.Address), failures);
        ValidateValue(options.Email, nameof(IdProofingRequirementsSettings.Email), failures);
        ValidateValue(options.Phone, nameof(IdProofingRequirementsSettings.Phone), failures);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }

    private static void ValidateValue(string? value, string propertyName, List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            failures.Add($"{propertyName} must be specified. Valid values: None, IAL1.");
            return;
        }

        if (!ValidValues.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            failures.Add($"{propertyName} has invalid value '{value}'. Valid values: None, IAL1.");
        }
    }
}
