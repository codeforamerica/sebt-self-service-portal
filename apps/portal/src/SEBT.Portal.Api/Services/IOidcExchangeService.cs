using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using SEBT.Portal.Core.Models.Auth;

namespace SEBT.Portal.Api.Services;

/// <summary>
/// Performs the OIDC token exchange and id_token verification at the API layer.
///
/// The service is stateless between requests (all flow state lives in the pre-auth session store).
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
    /// Fetches the cached OIDC discovery document for the configured IdP. Returns the
    /// <see cref="OpenIdConnectConfiguration"/> containing endpoint URLs (authorization,
    /// token, userinfo), signing keys, and issuer metadata.
    /// </summary>
    /// <param name="isStepUp">True to use the step-up IdP configuration.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    Task<OpenIdConnectConfiguration> GetDiscoveryConfigAsync(
        bool isStepUp,
        CancellationToken cancellationToken = default);
}
