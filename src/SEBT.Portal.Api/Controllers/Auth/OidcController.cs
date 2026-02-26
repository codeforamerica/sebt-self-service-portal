using System.Text;
using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services.StateAuth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models;

namespace SEBT.Portal.Api.Controllers.Auth;

/// <summary>
/// OIDC endpoints for state-specific login. Config is under Oidc:{stateCode} (e.g. Oidc:co:DiscoveryEndpoint).
/// </summary>
[ApiController]
[Route("api/auth/oidc")]
public class OidcController(
    IConfiguration config,
    IHttpClientFactory httpFactory,
    ILogger<OidcController> logger,
    IStateAuthStore store,
    IUserRepository userRepository,
    IJwtTokenService jwtService) : ControllerBase
{
    /// <summary>
    /// Public OIDC config for frontend PKCE flow (no secrets): authorization endpoint, token endpoint, client id, redirect URI.
    /// Config key: Oidc:{stateCode} (e.g. Oidc:co:DiscoveryEndpoint).
    /// </summary>
    [HttpGet("{code}/config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetConfig([FromRoute] string code, CancellationToken cancellationToken)
    {
        var stateKey = code.ToLowerInvariant();
        var discoveryEndpoint = config[$"Oidc:{stateKey}:DiscoveryEndpoint"];
        var clientId = config[$"Oidc:{stateKey}:ClientId"];
        var redirectUri = config[$"Oidc:{stateKey}:CallbackRedirectUri"];
        if (string.IsNullOrEmpty(discoveryEndpoint) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            logger.LogWarning("OIDC config missing for state {StateCode} (Oidc:{StateKey}:DiscoveryEndpoint, ClientId, or CallbackRedirectUri)", code, stateKey);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = $"OIDC not configured for {code}.",
                hint =
                    $"Set Oidc:{stateKey}:ClientId in appsettings (or env Oidc__{stateKey}__ClientId). DiscoveryEndpoint and CallbackRedirectUri must also be set."
            });
        }

        try
        {
            using var client = httpFactory.CreateClient();
            var discoveryJson = await client.GetStringAsync(discoveryEndpoint, cancellationToken).ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(discoveryJson);
            var root = doc.RootElement;
            var authEndpoint = root.TryGetProperty("authorization_endpoint", out var ae) ? ae.GetString() : null;
            var tokenEndpoint = root.TryGetProperty("token_endpoint", out var te) ? te.GetString() : null;
            if (string.IsNullOrEmpty(authEndpoint) || string.IsNullOrEmpty(tokenEndpoint))
                return StatusCode(StatusCodes.Status502BadGateway, new { error = "Invalid discovery document." });
            return Ok(new { authorizationEndpoint = authEndpoint, tokenEndpoint, clientId, redirectUri });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch OIDC discovery document for state {StateCode}", code);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Unable to load OIDC config." });
        }
    }

    /// <summary>
    /// Backend code exchange: frontend sends code and code_verifier; we exchange with IdP, validate id_token, and return portal JWT.
    /// Config key: Oidc:{stateCode} (e.g. Oidc:co:ClientSecret required for exchange).
    /// </summary>
    [HttpPost("{code}/exchange-code")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ExchangeCode(
        [FromRoute] string code,
        [FromBody] ExchangeCodeRequest? body,
        CancellationToken cancellationToken)
    {
        if (body == null || string.IsNullOrEmpty(body.Code) || string.IsNullOrEmpty(body.CodeVerifier))
            return BadRequest(new { error = "Missing code or code_verifier." });

        var stateKey = code.ToLowerInvariant();
        var discoveryEndpoint = config[$"Oidc:{stateKey}:DiscoveryEndpoint"];
        var clientId = config[$"Oidc:{stateKey}:ClientId"];
        var clientSecret = config[$"Oidc:{stateKey}:ClientSecret"];
        var redirectUri = config[$"Oidc:{stateKey}:CallbackRedirectUri"];
        if (string.IsNullOrEmpty(discoveryEndpoint) || string.IsNullOrEmpty(clientId) ||
            string.IsNullOrEmpty(redirectUri) || string.IsNullOrEmpty(clientSecret))
        {
            logger.LogWarning("OIDC config missing for state {StateCode} (Oidc:{StateKey}:DiscoveryEndpoint, ClientId, ClientSecret, or CallbackRedirectUri)", code, stateKey);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = $"OIDC not configured for {code} (ClientSecret required for exchange-code).",
                hint =
                    $"Set Oidc:{stateKey}:ClientSecret in appsettings (or env Oidc__{stateKey}__ClientSecret)."
            });
        }

        var oidc = HttpContext.RequestServices.GetKeyedService<IStateOidcLoginService>(stateKey);
        if (oidc == null)
        {
            logger.LogWarning("OIDC plugin not loaded for state {StateCode}; exchange-code requires the state connector.", code);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = $"OIDC plugin not available for {code}.",
                hint = "Ensure the state connector is built and its DLLs are in the Api project's plugins folder, then restart the API."
            });
        }

        try
        {
            using var client = httpFactory.CreateClient();
            var discoveryJson = await client.GetStringAsync(discoveryEndpoint, cancellationToken).ConfigureAwait(false);
            using var doc = System.Text.Json.JsonDocument.Parse(discoveryJson);
            var root = doc.RootElement;
            var tokenEndpoint = root.TryGetProperty("token_endpoint", out var te) ? te.GetString() : null;
            if (string.IsNullOrEmpty(tokenEndpoint))
                return StatusCode(StatusCodes.Status502BadGateway,
                    new { error = "Invalid discovery document (no token_endpoint)." });

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
            var form = new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = body.Code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = body.CodeVerifier!
            };
            using var formContent = new FormUrlEncodedContent(form);
            using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
            request.Content = formContent;
            using var tokenResponse = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var tokenJson = await tokenResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var tokenDoc = System.Text.Json.JsonDocument.Parse(tokenJson);
            var tokenRoot = tokenDoc.RootElement;

            if (tokenRoot.TryGetProperty("error", out var errProp))
            {
                var errDesc = tokenRoot.TryGetProperty("error_description", out var ed)
                    ? ed.GetString()
                    : errProp.GetString();
                logger.LogWarning("OIDC token exchange failed: {Error}", errDesc);
                return BadRequest(new { error = errDesc ?? "Token exchange failed." });
            }

            if (!tokenRoot.TryGetProperty("id_token", out var idTokenProp))
            {
                logger.LogWarning("OIDC token response had no id_token");
                return BadRequest(new { error = "No id_token in token response." });
            }

            var idToken = idTokenProp.GetString();
            if (string.IsNullOrEmpty(idToken))
                return BadRequest(new { error = "Empty id_token." });

            var authContext = await oidc.ValidateIdTokenAsync(idToken, cancellationToken);
            var sessionId = Guid.NewGuid().ToString("N");
            var expiration = TimeSpan.FromHours(1);
            await store.SetAsync(sessionId, authContext, expiration, cancellationToken);

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = !Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase),
                SameSite = SameSiteMode.Lax,
                Path = "/",
                MaxAge = expiration
            };
            Response.Cookies.Append(CookieStateAuthSessionAccessor.CookieName, sessionId, cookieOptions);

            var email = GetEmailFromClaims(authContext.IdTokenClaims);
            if (string.IsNullOrWhiteSpace(email))
            {
                logger.LogWarning("OIDC id_token had no email or sub claim");
                return BadRequest(new { error = "id_token must contain an email or sub claim." });
            }

            var normalizedEmail = EmailNormalizer.Normalize(email);
            var (user, _) = await userRepository.GetOrCreateUserAsync(normalizedEmail, cancellationToken);
            var token = jwtService.GenerateToken(user);
            return Ok(new { token });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "OIDC exchange-code failed for state {StateCode}", code);
            return BadRequest(new { error = "Code exchange or validation failed." });
        }
    }

    private static string? GetEmailFromClaims(IReadOnlyDictionary<string, object>? claims)
    {
        if (claims == null) return null;
        if (claims.TryGetValue("email", out var emailObj) && emailObj is string email)
            return email;
        if (claims.TryGetValue("sub", out var subObj) && subObj is string sub)
            return sub;
        return null;
    }
}
