using System.Globalization;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Translates external OIDC identity-verification claims (e.g. from PingOne/Socure)
/// into the portal's IAL model. Determines whether the verification is still valid
/// based on the configurable validity duration.
/// </summary>
public class OidcVerificationClaimTranslator
{
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
        if (!claims.TryGetValue(_claimSettings.LevelClaimName, out var levelValue)
            || string.IsNullOrWhiteSpace(levelValue))
        {
            return null;
        }

        var ialLevel = TranslateLevel(levelValue);
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

    private DateTime? ParseVerificationDate(IReadOnlyDictionary<string, string> claims)
    {
        if (!claims.TryGetValue(_claimSettings.DateClaimName, out var dateValue)
            || string.IsNullOrWhiteSpace(dateValue))
        {
            _logger.LogWarning("Missing expected OIDC ID verification claim \"{ClaimName}\"", _claimSettings.DateClaimName);
            return null;
        }

        if (!DateTime.TryParse(dateValue, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            _logger.LogWarning("Failed to parse OIDC ID verification claim \"{ClaimName}\". Value: {Value}",
                _claimSettings.DateClaimName, dateValue);
            return null;
        }

        return parsed;
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
