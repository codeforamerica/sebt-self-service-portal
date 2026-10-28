using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Api.Startup;

/// <summary>
/// Logs the configured OIDC verification claim names once at startup, so a missing fallback
/// is visible in the logs rather than only as users unexpectedly reading as unverified.
/// </summary>
public static class OidcVerificationClaimStartupLog
{
    private const string Unset = "(none)";

    /// <summary>Logs the verification claim names from the app's configured options.</summary>
    public static void LogOidcVerificationClaimNames(this WebApplication app)
    {
        Write(
            app.Services.GetRequiredService<IOptions<OidcSettings>>().Value,
            app.Services.GetRequiredService<IOptions<OidcVerificationClaimSettings>>().Value,
            app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(OidcVerificationClaimStartupLog)));
    }

    /// <summary>Logs the verification claim names when OIDC is enabled; does nothing otherwise.</summary>
    public static void Write(OidcSettings oidcSettings, OidcVerificationClaimSettings claimSettings, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(oidcSettings.DiscoveryEndpoint))
        {
            return;
        }

        logger.LogInformation(
            "OIDC verification claims: level {LevelClaimName} (fallback {FallbackLevelClaimName}), "
            + "date {DateClaimName} (fallback {FallbackDateClaimName})",
            claimSettings.LevelClaimName,
            OrUnset(claimSettings.FallbackLevelClaimName),
            claimSettings.DateClaimName,
            OrUnset(claimSettings.FallbackDateClaimName));
    }

    private static string OrUnset(string? claimName) =>
        string.IsNullOrWhiteSpace(claimName) ? Unset : claimName;
}
