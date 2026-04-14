using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Translates external OIDC identity-verification claims into the portal's IAL model.
/// Returns null when the claims contain no recognized verification level.
/// </summary>
public interface IOidcVerificationClaimTranslator
{
    /// <summary>
    /// Attempts to extract and translate OIDC verification claims into a portal IAL result.
    /// Returns <c>null</c> when the claims contain no recognized verification level.
    /// </summary>
    OidcVerificationResult? Translate(IReadOnlyDictionary<string, string> claims);
}

/// <summary>
/// Result of translating OIDC verification claims into the portal's IAL model.
/// </summary>
public record OidcVerificationResult(
    UserIalLevel IalLevel,
    DateTime? VerifiedAt,
    bool IsExpired);
