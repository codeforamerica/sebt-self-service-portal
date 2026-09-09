using System.Security.Claims;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Performs the OIDC token exchange and id_token verification entirely server-side.
/// The implementation owns the IdP HTTP calls, JWKS validation, and callback-token
/// signing/validation; UseCases handlers orchestrate it without seeing any of that.
///
/// Implementations are stateless between requests (all flow state lives in the
/// pre-auth session store). Inject as scoped or transient.
/// </summary>
public interface IOidcExchangeService
{
    /// <summary>
    /// Exchanges an authorization code for an id_token, verifies it, and signs a short-lived
    /// callback token containing the IdP claims. Returns the callback token on success.
    /// </summary>
    /// <param name="code">Authorization code from PingOne redirect.</param>
    /// <param name="codeVerifier">PKCE code_verifier (from the pre-auth session, never from the browser).</param>
    /// <param name="redirectUri">redirect_uri that was sent in the authorization request.</param>
    /// <param name="isStepUp">True when this is a step-up (IAL1+) flow.</param>
    /// <param name="sessionId">Pre-auth session id for correlated off-boarding logs.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Signed callback token (short-lived JWT) on success.</returns>
    Task<OidcExchangeResult> ExchangeCodeAsync(
        string code,
        string codeVerifier,
        string redirectUri,
        bool isStepUp,
        string? sessionId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches the endpoints the portal needs from the cached OIDC discovery document
    /// for the configured IdP. Throws when OIDC is not configured or the discovery
    /// document cannot be loaded — callers decide how to degrade.
    /// </summary>
    /// <param name="isStepUp">True to use the step-up IdP configuration.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<OidcDiscoveryInfo> GetDiscoveryInfoAsync(
        bool isStepUp,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a callback token previously signed by <see cref="ExchangeCodeAsync"/>
    /// (signature, issuer, audience, lifetime) and returns its claims. Signing and
    /// validation share one implementation so the key and issuer/audience rules can
    /// never drift apart.
    /// </summary>
    OidcCallbackTokenResult ValidateCallbackToken(string callbackToken);
}

/// <summary>
/// The discovery-document endpoints exposed to callers. Kept deliberately narrow so
/// consumers don't take a dependency on the OIDC protocol library's configuration type.
/// </summary>
public sealed record OidcDiscoveryInfo
{
    /// <summary>The IdP's <c>authorization_endpoint</c>, when advertised.</summary>
    public string? AuthorizationEndpoint { get; init; }

    /// <summary>The IdP's <c>end_session_endpoint</c> (RP-Initiated Logout), when advertised.</summary>
    public string? EndSessionEndpoint { get; init; }
}

/// <summary>Result of the OIDC code exchange.</summary>
public sealed record OidcExchangeResult
{
    /// <summary>True when exchange + verification succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Signed callback token (short-lived JWT containing IdP claims). Null on failure.</summary>
    public string? CallbackToken { get; init; }

    /// <summary>Phone claim value extracted during the exchange (for diagnostic logging). Null when absent or on failure.</summary>
    public string? PhoneClaim { get; init; }

    /// <summary>Human-readable error message for the client. Null on success.</summary>
    public string? Error { get; init; }

    /// <summary>Why the exchange failed. Null on success.</summary>
    public OidcExchangeFailureReason? FailureReason { get; init; }

    /// <summary>Creates a successful result with the given callback token.</summary>
    public static OidcExchangeResult Ok(string callbackToken, string? phoneClaim = null) => new()
    {
        Success = true,
        CallbackToken = callbackToken,
        PhoneClaim = phoneClaim
    };

    /// <summary>Creates a failed result with the given reason and error message.</summary>
    public static OidcExchangeResult Fail(
        OidcExchangeFailureReason reason,
        string error) => new()
        {
            Success = false,
            Error = error,
            FailureReason = reason
        };
}

/// <summary>
/// Failure categories for the OIDC code exchange. Callers translate these into their own
/// error semantics (the API layer maps them to HTTP status codes).
/// </summary>
public enum OidcExchangeFailureReason
{
    /// <summary>OIDC client credentials or signing key are not configured.</summary>
    NotConfigured,

    /// <summary>The IdP discovery document could not be loaded.</summary>
    DiscoveryUnavailable,

    /// <summary>The discovery document loaded but is missing required endpoints.</summary>
    DiscoveryInvalid,

    /// <summary>
    /// The exchange itself failed: the IdP rejected the code, the token response was
    /// malformed, or the id_token failed verification.
    /// </summary>
    ExchangeFailed
}

/// <summary>Result of validating a callback token.</summary>
public sealed record OidcCallbackTokenResult
{
    /// <summary>The validated token's claims. Null when validation failed.</summary>
    public ClaimsPrincipal? Principal { get; init; }

    /// <summary>Sanitized validation error, for off-boarding logs. Null on success.</summary>
    public string? Error { get; init; }

    /// <summary>True when the callback-token signing key is not configured.</summary>
    public bool NotConfigured { get; init; }

    /// <summary>True when the token validated successfully.</summary>
    public bool Success => Principal != null;
}
