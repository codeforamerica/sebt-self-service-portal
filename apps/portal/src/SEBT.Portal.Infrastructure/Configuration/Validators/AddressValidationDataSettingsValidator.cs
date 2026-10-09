using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Infrastructure.Services;

namespace SEBT.Portal.Infrastructure.Configuration.Validators;

/// <summary>
/// Fails startup when <see cref="AddressValidationDataSettings.BlockedAddressFile"/> names a CSV
/// that is not embedded, rather than letting blocked-address checks silently go empty.
/// </summary>
public sealed class AddressValidationDataSettingsValidator : IValidateOptions<AddressValidationDataSettings>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, AddressValidationDataSettings options)
    {
        if (string.IsNullOrWhiteSpace(options.BlockedAddressFile)
            || CsvBlockedAddressDataSource.EmbeddedFileExists(options.BlockedAddressFile))
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            $"AddressValidationData:BlockedAddressFile '{options.BlockedAddressFile}' does not match any CSV "
            + "embedded from SEBT.Portal.Infrastructure/BlockedAddresses.");
    }
}
