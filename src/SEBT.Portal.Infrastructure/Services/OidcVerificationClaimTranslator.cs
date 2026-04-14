using System.Globalization;
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

    public OidcVerificationClaimTranslator(
        OidcVerificationClaimSettings claimSettings,
        IdProofingValiditySettings validitySettings)
    {
        _claimSettings = claimSettings;
        _validitySettings = validitySettings;
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

        var verifiedAt = ParseVerificationDate(claims);
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
            1.5m => UserIalLevel.IAL1plus,
            _ => null
        };
    }

    private DateTime? ParseVerificationDate(IReadOnlyDictionary<string, string> claims)
    {
        if (!claims.TryGetValue(_claimSettings.DateClaimName, out var dateValue)
            || string.IsNullOrWhiteSpace(dateValue))
        {
            return null;
        }

        return DateTime.TryParse(dateValue, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed
            : null;
    }

    private bool IsExpired(DateTime? verifiedAt)
    {
        if (verifiedAt == null)
        {
            // No date available — treat as fresh (the provider confirmed the level
            // but didn't say when; safer to grant access and let the next login re-evaluate).
            return false;
        }

        var validUntil = verifiedAt.Value.AddYears((int)_validitySettings.ValidityYears);
        return DateTime.UtcNow >= validUntil;
    }
}

/// <summary>
/// Result of translating OIDC verification claims into the portal's IAL model.
/// </summary>
public record OidcVerificationResult(
    UserIalLevel IalLevel,
    DateTime? VerifiedAt,
    bool IsExpired);
