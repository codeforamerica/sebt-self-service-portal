using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
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
            var languageParam = config[$"Oidc:{stateKey}:LanguageParam"] ?? "en";
            return Ok(new { authorizationEndpoint = authEndpoint, tokenEndpoint, clientId, redirectUri, languageParam });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch OIDC discovery document for state {StateCode}", code);
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Unable to load OIDC config." });
        }
    }

    /// <summary>
    /// Completes OIDC login when the Next.js server has already exchanged the code and validated the id_token.
    /// Accepts a short-lived callbackToken (JWT signed with Oidc:CompleteLoginSigningKey) containing IdP claims; builds StateAuthContext, stores it, sets cookie, returns portal JWT.
    /// </summary>
    [HttpPost("complete-login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CompleteLogin(
        [FromBody] CompleteLoginRequest? body,
        CancellationToken cancellationToken)
    {
        if (body == null || string.IsNullOrEmpty(body.StateCode) || string.IsNullOrEmpty(body.CallbackToken))
            return BadRequest(new { error = "Missing stateCode or callbackToken." });

        var stateKey = body.StateCode.ToLowerInvariant();
        var signingKey = config["Oidc:CompleteLoginSigningKey"];
        if (string.IsNullOrEmpty(signingKey))
        {
            logger.LogWarning("Oidc:CompleteLoginSigningKey is not configured.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Complete-login not configured.",
                hint = "Set Oidc:CompleteLoginSigningKey (same value as Next.js OIDC_COMPLETE_LOGIN_SIGNING_KEY)."
            });
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            IssuerSigningKey = key
        };
        var handler = new JwtSecurityTokenHandler();
        handler.MapInboundClaims = false; // Preserve original JWT claim names (sub, email) so GetEmailFromClaims finds them
        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(body.CallbackToken, validationParams, out _);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid or expired callback token for state {StateCode}", body.StateCode);
            return BadRequest(new { error = "Invalid or expired callback token." });
        }

        var claimsDict = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in principal.Claims)
            claimsDict[claim.Type] = claim.Value;

        var authContext = new StateAuthContext(IdToken: string.Empty, AccessToken: string.Empty, IdTokenClaims: claimsDict);

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
            logger.LogWarning("Callback token had no email or sub claim");
            return BadRequest(new { error = "Callback token must contain an email or sub claim." });
        }

        var normalizedEmail = EmailNormalizer.Normalize(email);
        var (user, _) = await userRepository.GetOrCreateUserAsync(normalizedEmail, cancellationToken);
        var token = jwtService.GenerateToken(user);
        return Ok(new { token });
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
