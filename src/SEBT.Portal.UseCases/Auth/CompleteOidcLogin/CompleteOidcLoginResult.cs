namespace SEBT.Portal.UseCases.Auth;

/// <summary>
/// Successful result of <see cref="CompleteOidcLoginCommand"/>: a signed portal JWT
/// and the cookie expiration time. The controller uses these to set the HttpOnly auth cookie.
/// </summary>
public record CompleteOidcLoginResult(
    /// <summary>Signed portal JWT token string.</summary>
    string Token,
    /// <summary>When the auth cookie should expire.</summary>
    DateTimeOffset ExpiresAt,
    /// <summary>Safe relative return URL for step-up flows (null for normal login).</summary>
    string? ReturnUrl);
