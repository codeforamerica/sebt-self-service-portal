using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Infrastructure.Configuration.Validators;

public class IdentifierHasherSettingsValidator(IHostEnvironment environment)
    : IValidateOptions<IdentifierHasherSettings>
{
    private static readonly string[] ForbiddenKeys =
    [
        "OverrideInProductionUseEnvVarIDENTIFIERHASHER__SECRETKEY",
        "DevelopmentIdentifierHasherKeyMustBeAtLeast32CharactersLong"
    ];

    private const string ValidationFailedMessage =
        "IdentifierHasher:SecretKey must be set to a secure value in production. " +
        "Set the IDENTIFIERHASHER__SECRETKEY environment variable.";

    public ValidateOptionsResult Validate(string? name, IdentifierHasherSettings options)
    {
        if (!environment.IsProduction())
        {
            return ValidateOptionsResult.Skip;
        }

        if (string.IsNullOrEmpty(options.SecretKey))
        {
            return ValidateOptionsResult.Fail(ValidationFailedMessage);
        }

        if (ForbiddenKeys.Any(fk => string.Equals(options.SecretKey, fk, StringComparison.Ordinal)))
        {
            return ValidateOptionsResult.Fail(ValidationFailedMessage);
        }

        if (options.SecretKey.Contains("OverrideInProduction", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(ValidationFailedMessage);
        }

        return ValidateOptionsResult.Success;
    }
}
