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
    /// </summary>
    CallbackTokenValidationResult Validate(string callbackToken);
}

/// <summary>
/// Result of callback token validation. Check <see cref="IsSuccess"/> before accessing claims.
/// </summary>
public record CallbackTokenValidationResult
{
    /// <summary>True when validation succeeded and claims are available.</summary>
    public bool IsSuccess { get; init; }

    /// <summary>The user's email address extracted from the token (normalized). Null on failure.</summary>
    public string? Email { get; init; }

    /// <summary>Non-infrastructure IdP claims to pass through to the portal JWT. Null on failure.</summary>
    public IReadOnlyDictionary<string, string>? AdditionalClaims { get; init; }

    /// <summary>
    /// When validation failed, indicates whether the failure is a server-side configuration issue
    /// (e.g. missing signing key) rather than a client-provided invalid token.
    /// </summary>
    public bool IsServerError { get; init; }

    /// <summary>Human-readable error message for logging/debugging.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Creates a successful result with extracted claims.</summary>
    public static CallbackTokenValidationResult Success(string email, IReadOnlyDictionary<string, string> additionalClaims)
        => new() { IsSuccess = true, Email = email, AdditionalClaims = additionalClaims };

    /// <summary>Creates a failure result for an invalid/expired client-provided token.</summary>
    public static CallbackTokenValidationResult InvalidToken(string message)
        => new() { IsSuccess = false, ErrorMessage = message };

    /// <summary>Creates a failure result for a server-side configuration issue.</summary>
    public static CallbackTokenValidationResult ServerConfigError(string message)
        => new() { IsSuccess = false, IsServerError = true, ErrorMessage = message };
}
