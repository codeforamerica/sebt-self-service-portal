namespace SEBT.Portal.Core.Services;

/// <summary>
/// Validates an OIDC callback token JWT and extracts claims.
/// The implementation handles signing key resolution, issuer/audience validation,
/// and claim extraction — keeping JWT infrastructure out of the UseCases layer.
/// </summary>
public interface ICallbackTokenValidator
{
    /// <summary>
    /// Validates the callback token and extracts non-infrastructure claims.
    /// Returns null if the token is invalid, expired, or malformed.
    /// </summary>
    CallbackTokenValidationResult? Validate(string callbackToken);
}

/// <summary>
/// The validated output of a callback token: the user's email and passthrough claims.
/// </summary>
public record CallbackTokenValidationResult(
    /// <summary>The user's email address extracted from the token (normalized).</summary>
    string Email,
    /// <summary>Non-infrastructure IdP claims to pass through to the portal JWT.</summary>
    IReadOnlyDictionary<string, string> AdditionalClaims);
