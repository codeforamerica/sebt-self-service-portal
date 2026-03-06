using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Api.Controllers.Auth;

/// <summary>
/// OIDC endpoints for state IdP login. Config is under Oidc.
/// </summary>
[ApiController]
[Route("api/auth/oidc")]
public class OidcController(
    IConfiguration config,
    IHttpClientFactory httpFactory,
    ILogger<OidcController> logger,
    IUserRepository userRepository,
    IJwtTokenService jwtService) : ControllerBase
{
    /// <summary>
    /// Standard OIDC/JWT and IdP-infrastructure claim names to exclude when copying IdP claims into the portal JWT.
    /// All other claims (for example: phone, givenName, familyName, email, sub, userId) are added to the portal token.
    /// </summary>
    private static readonly HashSet<string> CommonIdpClaimNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "iss", "aud", "iat", "exp", "nbf",
        "acr", "amr", "auth_time", "at_hash", "sid",
        "env", "org", "p1.region"
    };
    /// <summary>
    /// Public OIDC config for frontend PKCE flow (no secrets): authorization endpoint, token endpoint, client id, redirect URI.
    /// Config keys: Oidc:DiscoveryEndpoint, Oidc:ClientId, Oidc:CallbackRedirectUri.
    /// </summary>
    [HttpGet("{code}/config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetConfig([FromRoute] string code, CancellationToken cancellationToken)
    {
        var discoveryEndpoint = config["Oidc:DiscoveryEndpoint"];
        var clientId = config["Oidc:ClientId"];
        var redirectUri = config["Oidc:CallbackRedirectUri"];
        if (string.IsNullOrEmpty(discoveryEndpoint) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            logger.LogWarning("OIDC config missing (Oidc:DiscoveryEndpoint, ClientId, or CallbackRedirectUri)");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "OIDC not configured.",
                hint = "Set Oidc:DiscoveryEndpoint, Oidc:ClientId, and Oidc:CallbackRedirectUri in appsettings."
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
            var languageParam = config["Oidc:LanguageParam"] ?? "en";
            return Ok(new { authorizationEndpoint = authEndpoint, tokenEndpoint, clientId, redirectUri, languageParam });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch OIDC discovery document");
            return StatusCode(StatusCodes.Status502BadGateway, new { error = "Unable to load OIDC config." });
        }
    }

    /// <summary>
    /// Completes OIDC login when the Next.js server has already exchanged the code and validated the id_token.
    /// Accepts a short-lived callbackToken (JWT signed with Oidc:CompleteLoginSigningKey) containing IdP claims;
    /// copies non-common IdP claims into the portal JWT and returns it.
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
        handler.MapInboundClaims = false; // Preserve original JWT claim names (sub, email)
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

        // Copy non-common IdP claims into the portal JWT (e.g. phone, givenName, familyName, userId, email, sub)
        var additionalClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in principal.Claims)
        {
            if (!CommonIdpClaimNames.Contains(claim.Type) && !string.IsNullOrEmpty(claim.Value))
                additionalClaims[claim.Type] = claim.Value;
        }

        var email = GetEmailFromClaims(principal);
        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("Callback token had no email claim");
            return BadRequest(new { error = "Callback token must contain an email claim." });
        }

        var normalizedEmail = EmailNormalizer.Normalize(email);
        var (user, _) = await userRepository.GetOrCreateUserAsync(normalizedEmail, cancellationToken);
        var token = jwtService.GenerateToken(user, additionalClaims);
        return Ok(new { token });
    }

    /// <summary>
    /// Gets the email from the callback token claims.
    /// </summary>
    private static string? GetEmailFromClaims(ClaimsPrincipal principal)
    {
        var emailClaim = principal.FindFirst("email");
        return !string.IsNullOrEmpty(emailClaim?.Value) ? emailClaim.Value : null;
    }
}
