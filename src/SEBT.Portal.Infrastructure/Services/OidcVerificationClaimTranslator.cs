using System.Globalization;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Translates external OIDC identity-verification claims (e.g. from PingOne/Socure)
/// into the portal's IAL model. Determines whether the verification is still valid
/// based on the configurable validity duration.
/// When the configured <see cref="OidcVerificationClaimSettings.LevelClaimName"/> is missing,
/// empty, or not a recognized level, the translator tries <see cref="OidcVerificationClaimSettings.FallbackLevelClaimName"/>
/// unless it duplicates <c>LevelClaimName</c>.
/// Likewise for verification date via <see cref="OidcVerificationClaimSettings.FallbackDateClaimName"/>
/// </summary>
public class OidcVerificationClaimTranslator
{
    /// <summary>
    /// Default secondary OIDC claim name for verification level when
    /// <see cref="OidcVerificationClaimSettings.FallbackLevelClaimName"/> is unset (myColorado: <c>myCoIdVerificationLevel</c>).
    /// </summary>
    internal const string DefaultFallbackLevelClaimName = "myCoIdVerificationLevel";

    /// <summary>
    /// Default secondary OIDC claim name for verification date when
    /// <see cref="OidcVerificationClaimSettings.FallbackDateClaimName"/> is unset.
    /// </summary>
    internal const string DefaultFallbackDateClaimName = "myCoIdVerificationDate";

    private readonly OidcVerificationClaimSettings _claimSettings;
    private readonly IdProofingValiditySettings _validitySettings;
    private readonly ILogger<OidcVerificationClaimTranslator> _logger;

    public OidcVerificationClaimTranslator(
        OidcVerificationClaimSettings claimSettings,
        IdProofingValiditySettings validitySettings,
        ILogger<OidcVerificationClaimTranslator> logger)
    {
        _claimSettings = claimSettings;
        _validitySettings = validitySettings;
        _logger = logger;
    }

    /// <summary>
    /// Attempts to extract and translate OIDC verification claims into a portal IAL result.
    /// Returns <c>null</c> when the claims contain no recognized verification level.
    /// </summary>
    public OidcVerificationResult? Translate(IReadOnlyDictionary<string, string> claims)
    {
        var ialLevel = ResolveIalLevel(claims);
        if (ialLevel == null)
        {
            return null;
        }

        // If the IdP doesn't include a verification date, use the current time so the
        // expiration clock starts now. This prevents indefinite IAL1+ without a bounded validity.
        var verifiedAt = ParseVerificationDate(claims) ?? DateTime.UtcNow;
        var isExpired = IsExpired(verifiedAt);

        return new OidcVerificationResult(ialLevel.Value, verifiedAt, isExpired);
    }

    /// <summary>
    /// Prefers <see cref="OidcVerificationClaimSettings.LevelClaimName"/> when it yields a recognized level;
    /// otherwise tries the resolved fallback claim name if distinct from the configured primary name.
    /// </summary>
    private UserIalLevel? ResolveIalLevel(IReadOnlyDictionary<string, string> claims)
    {
        if (TryGetNonEmptyClaimValue(claims, _claimSettings.LevelClaimName, out var primaryLevel))
        {
            var primary = TranslateLevel(primaryLevel);
            if (primary != null)
            {
                return primary;
            }
        }

        var fallbackLevelClaimName = ResolvedFallbackLevelClaimName();
        if (ClaimNamesAreEquivalent(_claimSettings.LevelClaimName, fallbackLevelClaimName))
        {
            return null;
        }

        if (!TryGetNonEmptyClaimValue(claims, fallbackLevelClaimName, out var fallbackLevel))
        {
            return null;
        }

        return TranslateLevel(fallbackLevel);
    }

    private string ResolvedFallbackLevelClaimName() =>
        string.IsNullOrWhiteSpace(_claimSettings.FallbackLevelClaimName)
            ? DefaultFallbackLevelClaimName
            : _claimSettings.FallbackLevelClaimName.Trim();

    private string ResolvedFallbackDateClaimName() =>
        string.IsNullOrWhiteSpace(_claimSettings.FallbackDateClaimName)
            ? DefaultFallbackDateClaimName
            : _claimSettings.FallbackDateClaimName.Trim();

    private static bool ClaimNamesAreEquivalent(string a, string b) =>
        string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static bool TryGetNonEmptyClaimValue(
        IReadOnlyDictionary<string, string> claims,
        string claimName,
        out string value)
    {
        if (!claims.TryGetValue(claimName, out var raw) || raw is null || string.IsNullOrWhiteSpace(raw))
        {
            value = "";
            return false;
        }

        value = raw;
        return true;
    }

    private static UserIalLevel? TranslateLevel(string value)
    {
        // Normalize: trim and parse as decimal to handle "1.5", "1.50", etc.
        if (!decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var numeric))
        {
            return null;
        }

        return numeric switch
        {
            1m => UserIalLevel.IAL1,
            1.5m => UserIalLevel.IAL1plus,
            2m => UserIalLevel.IAL2,
            _ => null
        };
    }

    /// <summary>
    /// Prefers <see cref="OidcVerificationClaimSettings.DateClaimName"/> when present and parseable;
    /// otherwise tries the resolved fallback claim name if distinct from the configured primary name.
    /// </summary>
    private DateTime? ParseVerificationDate(IReadOnlyDictionary<string, string> claims)
    {
        if (TryParseVerificationDateFromClaim(claims, _claimSettings.DateClaimName, out var parsed))
        {
            return parsed;
        }

        var fallbackDateClaimName = ResolvedFallbackDateClaimName();
        if (!ClaimNamesAreEquivalent(_claimSettings.DateClaimName, fallbackDateClaimName))
        {
            if (TryParseVerificationDateFromClaim(claims, fallbackDateClaimName, out parsed))
            {
                return parsed;
            }
        }

        _logger.LogWarning(
            "OIDC ID verification date could not be resolved from claim \"{Primary}\" or fallback \"{Fallback}\".",
            _claimSettings.DateClaimName,
            fallbackDateClaimName);
        return null;
    }

    /// <returns><c>true</c> when the claim exists, is non-empty, and parses as a UTC-adjusted date/time.</returns>
    private bool TryParseVerificationDateFromClaim(
        IReadOnlyDictionary<string, string> claims,
        string claimName,
        out DateTime parsed)
    {
        parsed = default;
        if (!TryGetNonEmptyClaimValue(claims, claimName, out var dateValue))
        {
            return false;
        }

        if (!DateTime.TryParse(dateValue, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out parsed))
        {
            return false;
        }

        return true;
    }

    private bool IsExpired(DateTime verifiedAt)
    {
        var validUntil = verifiedAt.AddDays(_validitySettings.ValidityDays);
        return DateTime.UtcNow >= validUntil;
    }
}

/// <summary>
/// Result of translating OIDC verification claims into the portal's IAL model.
/// </summary>
public record OidcVerificationResult(
    UserIalLevel IalLevel,
    DateTime VerifiedAt,
    bool IsExpired);
