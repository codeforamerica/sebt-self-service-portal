namespace SEBT.Portal.Api.Startup;

/// <summary>
/// Validates that the IdentifierHasher secret key is not a default or placeholder value in production.
/// </summary>
public static class IdentifierHasherGuard
{
    private static readonly string[] ForbiddenKeys =
    [
        "OverrideInProductionUseEnvVarIDENTIFIERHASHER__SECRETKEY",
        "DevelopmentIdentifierHasherKeyMustBeAtLeast32CharactersLong"
    ];

    /// <summary>
    /// Validates the secret key for production use. Throws if the key is a forbidden placeholder.
    /// </summary>
    /// <param name="secretKey">The configured secret key value.</param>
    /// <exception cref="InvalidOperationException">Thrown when the key is empty or a known placeholder.</exception>
    public static void ValidateForProduction(string? secretKey)
    {
        if (string.IsNullOrEmpty(secretKey) ||
            ForbiddenKeys.Any(fk => string.Equals(secretKey, fk, StringComparison.Ordinal)) ||
            secretKey.Contains("OverrideInProduction", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "IdentifierHasher:SecretKey must be set to a secure value in production. " +
                "Set the IDENTIFIERHASHER__SECRETKEY environment variable.");
        }
    }
}
