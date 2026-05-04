using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration;

/// <summary>
/// Ensures <see cref="PiiEncryptionSettings"/> binds coherently: key ring parses, lengths, and ActiveKeyId resolves.
/// </summary>
public sealed class PiiEncryptionSettingsValidator : IValidateOptions<PiiEncryptionSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, PiiEncryptionSettings options)
    {
        if (options == null)
        {
            return ValidateOptionsResult.Fail("PiiEncryption configuration section is not present.");
        }

        try
        {
            _ = options.ResolveKeyRing();
        }
        catch (Exception ex)
        {
            return ValidateOptionsResult.Fail($"PiiEncryption configuration is invalid: {ex.Message}");
        }

        return ValidateOptionsResult.Success;
    }
}
