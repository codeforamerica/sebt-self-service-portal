using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Api.Controllers.Auth;

/// <summary>
/// OIDC endpoints for external IdP login and step-up. Primary config uses flat <c>Oidc</c> keys
/// (<c>DiscoveryEndpoint</c>, <c>ClientId</c>, <c>CallbackRedirectUri</c>); optional <c>Oidc:StepUp:*</c>
/// selects a second client for elevated verification when <c>stepUp=true</c> on the config endpoint.
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
    /// Config keys: <c>Oidc:DiscoveryEndpoint</c>, <c>Oidc:ClientId</c>, <c>Oidc:CallbackRedirectUri</c>.
    /// When <c>stepUp=true</c>, uses <c>Oidc:StepUp:*</c> (second client / discovery for elevated verification).
    /// </summary>
    [HttpGet("{code}/config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetConfig(
        [FromRoute] string code,
        [FromQuery] bool stepUp = false,
        CancellationToken cancellationToken = default)
    {
        var discoveryEndpoint = stepUp
            ? config["Oidc:StepUp:DiscoveryEndpoint"]
            : config["Oidc:DiscoveryEndpoint"];
        var clientId = stepUp ? config["Oidc:StepUp:ClientId"] : config["Oidc:ClientId"];
        var redirectUri = stepUp
            ? (config["Oidc:StepUp:RedirectUri"] ?? config["Oidc:CallbackRedirectUri"])
            : config["Oidc:CallbackRedirectUri"];
        if (string.IsNullOrEmpty(discoveryEndpoint) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            logger.LogWarning("OIDC config missing (Oidc:DiscoveryEndpoint, Oidc:ClientId, or Oidc:CallbackRedirectUri)");
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
                return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse("Invalid discovery document."));
            var languageParam = config["Oidc:LanguageParam"] ?? "en";
            return Ok(new { authorizationEndpoint = authEndpoint, tokenEndpoint, clientId, redirectUri, languageParam });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch OIDC discovery document");
            return StatusCode(StatusCodes.Status502BadGateway, new ErrorResponse("Unable to load OIDC config."));
        }
    }

    /// <summary>
    /// Completes OIDC login when the Next.js server has already exchanged the code and validated the id_token.
    /// Accepts a short-lived callbackToken (JWT signed with Oidc:CompleteLoginSigningKey) containing IdP claims;
    /// copies non-common IdP claims into the portal JWT and returns it.
    /// Step-up may echo <c>returnUrl</c> only when it is a safe relative path (see <see cref="TrySanitizeStepUpReturnUrl"/>).
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
            return BadRequest(new ErrorResponse("Missing stateCode or callbackToken."));

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
            logger.LogWarning(ex, "Invalid or expired callback token for state {StateCode}",
                SanitizeForLog(body.StateCode));
            return BadRequest(new ErrorResponse("Invalid or expired callback token."));
        }

        // Copy non-common IdP claims into the portal JWT (e.g. phone, givenName, familyName, userId, email, sub)
        var additionalClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in principal.Claims)
        {
            if (!CommonIdpClaimNames.Contains(claim.Type) && !string.IsNullOrEmpty(claim.Value))
            {
                additionalClaims[claim.Type] = claim.Value;
            }
        }

        logger.LogInformation("Additional OIDC claim types: {Claims}", string.Join(", ", additionalClaims.Select(c => c.Key).ToArray()));

        if (!additionalClaims.Select(c => c.Key).Contains("phone"))
        {
            logger.LogWarning("OIDC incoming claims missing 'phone'");
        }

        var email = GetEmailFromClaims(principal);
        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("Callback token had no email or sub claim");
            return BadRequest(new ErrorResponse("Callback token must contain an email or sub claim."));
        }

        var normalizedEmail = EmailNormalizer.Normalize(email);
        User user;

        if (body.IsStepUp)
        {
            var existingUser = await userRepository.GetUserByEmailAsync(normalizedEmail, cancellationToken);
            if (existingUser == null)
            {
                logger.LogWarning("Step-up complete-login: no existing portal user for callback token; sign-in required first.");
                return BadRequest(new { error = "Step-up requires an existing session. Please sign in again." });
            }

            user = existingUser;
            user.IalLevel = UserIalLevel.IAL1plus;
            user.IdProofingStatus = IdProofingStatus.Completed;
            user.IdProofingCompletedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;
            await userRepository.UpdateUserAsync(user, cancellationToken);

            var safeStateKey = SanitizeForLog(stateKey);
            logger.LogInformation(
                "OIDC step-up complete-login succeeded: UserId {UserId}, StateCode {StateCode}, IalLevel {IalLevel}, IdProofingStatus {IdProofingStatus}",
                user.Id,
                safeStateKey,
                user.IalLevel,
                user.IdProofingStatus);
        }
        else
        {
            var (createdUser, _) = await userRepository.GetOrCreateUserAsync(normalizedEmail, cancellationToken);
            user = createdUser;

            // A user who completed OIDC login is at least IAL1; don't downgrade if already higher
            if (user.IalLevel < UserIalLevel.IAL1)
            {
                user.IalLevel = UserIalLevel.IAL1;
                await userRepository.UpdateUserAsync(user, cancellationToken);
            }
        }

        var token = jwtService.GenerateToken(user, additionalClaims);
        if (!body.IsStepUp)
            return Ok(new { token });

        var safeReturnUrl = TrySanitizeStepUpReturnUrl(body.ReturnUrl);
        if (safeReturnUrl != null)
            return Ok(new { token, returnUrl = safeReturnUrl });

        if (!string.IsNullOrWhiteSpace(body.ReturnUrl))
            logger.LogWarning("Step-up complete-login: returnUrl rejected (must be a safe relative path).");

        return Ok(new { token });
    }

    private const int MaxStepUpReturnUrlLength = 4096;

    /// <summary>
    /// Step-up post-login navigation: only same-document relative paths (for example <c>/profile/address</c>).
    /// Rejects absolute URLs and scheme-relative paths so the API never echoes an open redirect.
    /// </summary>
    private static string? TrySanitizeStepUpReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return null;
        var t = returnUrl.Trim();
        if (t.Length > MaxStepUpReturnUrlLength)
            return null;
        if (!t.StartsWith("/", StringComparison.Ordinal))
            return null;
        if (t.StartsWith("//", StringComparison.Ordinal))
            return null;
        var pathPart = t;
        var qIdx = t.IndexOf('?', StringComparison.Ordinal);
        if (qIdx >= 0)
            pathPart = t[..qIdx];
        if (pathPart.Contains("://", StringComparison.Ordinal))
            return null;
        if (t.Contains("\\", StringComparison.Ordinal))
            return null;
        if (t.Contains("\r", StringComparison.Ordinal) || t.Contains("\n", StringComparison.Ordinal)
            || t.Contains("\0", StringComparison.Ordinal))
            return null;
        return t;
    }

    /// <summary>
    /// Removes newline/control-friendly breaks from values logged from user input.
    /// </summary>
    private static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Replace("\n", string.Empty, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets the email (or subject) from the callback token claims for portal user lookup.
    /// </summary>
    private static string? GetEmailFromClaims(ClaimsPrincipal principal)
    {
        var emailClaim = principal.FindFirst("email");
        if (!string.IsNullOrEmpty(emailClaim?.Value))
            return emailClaim.Value;
        var subClaim = principal.FindFirst("sub");
        return subClaim?.Value;
    }
}
