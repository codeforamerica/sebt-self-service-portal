using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration.Validators;

/// <summary>
/// Ensures <see cref="PiiEncryptionSettings"/> binds coherently: key ring parses, 256-bit key lengths, and ActiveKeyId resolves.
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

        if (options.RunStartupBackfill && !options.EncryptAtRest)
        {
            return ValidateOptionsResult.Fail(
                "PiiEncryption:RunStartupBackfill requires EncryptAtRest to be true.");
        }

        if (!options.EncryptAtRest)
        {
            return ValidateOptionsResult.Success;
        }

        if (!options.HasKeyRingConfiguration())
        {
            return ValidateOptionsResult.Fail(
                "PiiEncryption:ActiveKeyId and PiiEncryption:Keys are required when EncryptAtRest is true.");
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
