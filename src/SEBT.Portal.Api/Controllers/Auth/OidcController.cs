using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Infrastructure.Services;

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
    ILogger<OidcController> logger,
    IUserRepository userRepository,
    IJwtTokenService jwtService,
    IOptions<JwtSettings> jwtSettingsOptions,
    IStateAllowlist stateAllowlist,
    IPreAuthSessionStore sessionStore,
    IWebHostEnvironment environment,
    OidcVerificationClaimTranslator verificationClaimTranslator) : ControllerBase
{
    /// <summary>
    /// OIDC config + pre-auth session creation. Generates PKCE server-side, stores
    /// <c>state</c> + <c>code_verifier</c> + <c>stateCode</c> in the session store, sets an
    /// <c>oidc_session</c> HttpOnly cookie, and returns only the <c>code_challenge</c> +
    /// <c>state</c> to the browser. The <c>code_verifier</c> never leaves the server.
    /// </summary>
    /// <remarks>
    /// The authorization endpoint is served from a pinned appsettings key, not from the
    /// IdP discovery document, so it cannot be manipulated by a rogue proxy or DNS attack.
    /// </remarks>
    [HttpGet("{code}/config")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetConfig(
        [FromRoute] string code,
        [FromQuery] bool stepUp = false,
        CancellationToken cancellationToken = default)
    {
        // Resolve the route parameter to the canonical allowlist value. TryResolve returns
        // a value from the allowlist itself (not derived from user input), breaking the
        // taint chain for CodeQL's "user input in log" analysis.
        var stateCode = stateAllowlist.TryResolve(code);
        if (stateCode == null)
        {
            logger.LogWarning("OIDC GetConfig rejected: unknown stateCode (reason=unknown_state)");
            return BadRequest(new ErrorResponse("Unknown or unsupported stateCode."));
        }

        var authorizationEndpoint = stepUp
            ? config["Oidc:StepUp:AuthorizationEndpoint"]
            : config["Oidc:AuthorizationEndpoint"];
        var clientId = stepUp ? config["Oidc:StepUp:ClientId"] : config["Oidc:ClientId"];
        var redirectUri = stepUp
            ? (config["Oidc:StepUp:RedirectUri"] ?? config["Oidc:CallbackRedirectUri"])
            : config["Oidc:CallbackRedirectUri"];
        if (string.IsNullOrEmpty(authorizationEndpoint) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            logger.LogWarning(
                "OIDC config missing for stateCode {StateCode} (reason=oidc_not_configured)",
                stateCode);
            var hint = environment.IsDevelopment()
                ? "Set Oidc:AuthorizationEndpoint, Oidc:ClientId, and Oidc:CallbackRedirectUri in appsettings."
                : "";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "OIDC not configured.", hint });
        }

        // Generate PKCE server-side — code_verifier never leaves the server.
        var codeVerifier = PkceHelper.GenerateCodeVerifier();
        var codeChallenge = PkceHelper.ComputeCodeChallenge(codeVerifier);
        var state = PkceHelper.GenerateState();

        // Create the pre-auth session and set the cookie.
        var session = await sessionStore.CreateAsync(
            stateCode, state, codeVerifier, redirectUri, stepUp, cancellationToken);
        OidcSessionCookie.Set(Response, session.Id);

        var languageParam = config["Oidc:LanguageParam"] ?? "en";
        return Ok(new
        {
            authorizationEndpoint,
            clientId,
            redirectUri,
            languageParam,
            state,
            codeChallenge,
            codeChallengeMethod = "S256"
        });
    }

    /// <summary>
    /// Server-side OIDC callback. Requires the <c>oidc_session</c> cookie to
    /// locate the pre-auth session. Validates <c>state</c> against the stored value,
    /// uses the stored <c>code_verifier</c> for the token exchange (never from the
    /// request body), and advances the session to <c>CallbackCompleted</c>.
    /// </summary>
    [HttpPost("callback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Callback(
        [FromBody] OidcCallbackRequest? body,
        [FromServices] IOidcExchangeService exchangeService,
        CancellationToken cancellationToken)
    {
        if (body == null || string.IsNullOrEmpty(body.Code) || string.IsNullOrEmpty(body.StateCode))
            return BadRequest(new ErrorResponse("Missing code or stateCode."));

        // Resolve stateCode to the canonical allowlist value (breaks taint chain).
        var requestedCode = body.Code;
        var requestedStateCode = stateAllowlist.TryResolve(body.StateCode);
        if (requestedStateCode == null)
        {
            logger.LogWarning("OIDC Callback rejected: unknown stateCode (reason=unknown_state)");
            return BadRequest(new ErrorResponse("Unknown or unsupported stateCode."));
        }

        // --- Require the oidc_session cookie ---
        var sessionId = OidcSessionCookie.Read(Request);
        if (string.IsNullOrEmpty(sessionId))
        {
            logger.LogWarning("OIDC Callback rejected: missing oidc_session cookie (reason=missing_session)");
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Missing pre-auth session."));
        }

        var session = await sessionStore.GetAsync(sessionId, cancellationToken);
        if (session == null)
        {
            logger.LogWarning("OIDC Callback rejected: session {SessionId} not found or expired (reason=missing_session)", sessionId);
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Pre-auth session expired or invalid."));
        }

        // --- Validate state matches stored value (CSRF protection) ---
        if (string.IsNullOrEmpty(body.State) || body.State != session.State)
        {
            logger.LogWarning(
                "OIDC Callback rejected: state mismatch (reason=mismatched_state, SessionId={SessionId})", sessionId);
            return BadRequest(new ErrorResponse("State parameter mismatch."));
        }

        // --- Validate stateCode matches stored value (prevents tenant switching) ---
        if (!string.Equals(requestedStateCode, session.StateCode, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "OIDC Callback rejected: stateCode mismatch (reason=mismatched_stateCode, SessionId={SessionId})", sessionId);
            return BadRequest(new ErrorResponse("State code mismatch."));
        }

        // --- Verify the session hasn't already been used (fail fast before the exchange) ---
        if (session.Phase != PreAuthSessionPhase.Created)
        {
            logger.LogWarning(
                "OIDC Callback rejected: session already used, Phase={Phase} (reason=replay, SessionId={SessionId})",
                session.Phase, sessionId);
            return BadRequest(new ErrorResponse("Pre-auth session has already been used."));
        }

        // --- Exchange code using the stored code_verifier (never from the body) ---
        var result = await exchangeService.ExchangeCodeAsync(
            requestedCode,
            session.CodeVerifier,
            session.RedirectUri,
            session.IsStepUp,
            cancellationToken);

        if (!result.Success)
        {
            logger.LogWarning(
                "OIDC Callback exchange failed: {Error} (reason=exchange_failed, SessionId={SessionId})",
                result.Error, sessionId);
            return StatusCode(result.StatusCode, new ErrorResponse(result.Error ?? "Exchange failed."));
        }

        // --- Advance session to CallbackCompleted and store the callback token hash ---
        var tokenHash = IPreAuthSessionStore.HashCallbackToken(result.CallbackToken!);
        var advanced = await sessionStore.TryAdvanceToCallbackCompletedAsync(sessionId, tokenHash, cancellationToken);
        if (!advanced)
        {
            logger.LogWarning(
                "OIDC Callback rejected: session could not advance (reason=replay, SessionId={SessionId})", sessionId);
            return BadRequest(new ErrorResponse("Pre-auth session has already been used."));
        }

        return Ok(new { callbackToken = result.CallbackToken });
    }

    /// <summary>
    /// Completes OIDC login. Requires the <c>oidc_session</c> cookie to locate
    /// the pre-auth session. Verifies the callback token was issued for this session and
    /// has not been used before. On success, mints the portal JWT, marks the session
    /// consumed, and clears the pre-auth cookie.
    /// </summary>
    [HttpPost("complete-login")]
    [ProducesResponseType(typeof(CompleteLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CompleteLogin(
        [FromBody] CompleteLoginRequest body,
        CancellationToken cancellationToken)
    {
        // Resolve stateCode via allowlist — returns a canonical value from the allowlist
        // itself, not derived from user input. Null if missing or not in the allowlist.
        var requestedStateCode = stateAllowlist.TryResolve(body.StateCode);
        if (requestedStateCode == null)
            return BadRequest(new ErrorResponse("Missing or unsupported stateCode."));

        // Bind callbackToken after null check; the token is validated cryptographically
        // (signature + hash match) before any sensitive action.
        if (string.IsNullOrEmpty(body.CallbackToken))
            return BadRequest(new ErrorResponse("Missing callbackToken."));
        var callbackToken = body.CallbackToken;

        // --- Require the oidc_session cookie ---
        var sessionId = OidcSessionCookie.Read(Request);
        if (string.IsNullOrEmpty(sessionId))
        {
            logger.LogWarning("OIDC CompleteLogin rejected: missing oidc_session cookie (reason=missing_session)");
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Missing pre-auth session."));
        }

        // --- Retrieve session to cross-check stateCode and read IsStepUp ---
        var session = await sessionStore.GetAsync(sessionId, cancellationToken);
        if (session == null)
        {
            logger.LogWarning("OIDC CompleteLogin rejected: session not found (reason=missing_session, SessionId={SessionId})", sessionId);
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Pre-auth session invalid, expired, or already used."));
        }

        // --- Validate stateCode matches stored value (prevents tenant switching) ---
        if (!string.Equals(requestedStateCode, session.StateCode, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "OIDC CompleteLogin rejected: stateCode mismatch (reason=mismatched_stateCode, SessionId={SessionId})", sessionId);
            return BadRequest(new ErrorResponse("State code mismatch."));
        }

        // --- Verify the callback token matches this session and hasn't been consumed ---
        var tokenHash = IPreAuthSessionStore.HashCallbackToken(callbackToken);
        var advanced = await sessionStore.TryAdvanceToLoginCompletedAsync(sessionId, tokenHash, cancellationToken);
        if (!advanced)
        {
            logger.LogWarning(
                "OIDC CompleteLogin rejected: session advance failed (reason=replay, SessionId={SessionId})", sessionId);
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Pre-auth session invalid, expired, or already used."));
        }

        // Clear the pre-auth cookie and remove the session from cache (defense-in-depth:
        // even if the phase check were bypassed, the code_verifier is gone from memory).
        OidcSessionCookie.Clear(Response);
        await sessionStore.RemoveAsync(sessionId, cancellationToken);

        var stateKey = session.StateCode.ToLowerInvariant();
        var signingKey = config["Oidc:CompleteLoginSigningKey"];
        if (string.IsNullOrEmpty(signingKey))
        {
            logger.LogWarning("Oidc:CompleteLoginSigningKey is not configured.");
            var hint = environment.IsDevelopment() ? "Set Oidc:CompleteLoginSigningKey in appsettings." : "";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Complete-login not configured.", hint });
        }

        var portalOrigin = config["Oidc:CallbackRedirectUri"]?.TrimEnd('/') ?? "sebt-portal";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var validationParams = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidIssuer = portalOrigin,
            ValidateAudience = true,
            ValidAudience = portalOrigin,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            // Use resolver instead of IssuerSigningKey to bypass kid-matching;
            // the callback token is signed without a kid header, which causes IDX10517
            // when JwtSecurityTokenHandler tries to match by kid.
            IssuerSigningKeyResolver = (token, securityToken, kid, parameters) => [key]
        };
        var handler = new JwtSecurityTokenHandler();
        handler.MapInboundClaims = false; // Preserve original JWT claim names (sub, email)
        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(callbackToken, validationParams, out _);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Invalid or expired callback token for state {StateCode}",
                SanitizeForLog(requestedStateCode));
            return BadRequest(new ErrorResponse("Invalid or expired callback token."));
        }

        // Copy non-common IdP claims into the portal JWT (e.g. phone, givenName, familyName, userId, email, sub)
        var additionalClaims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var claim in principal.Claims)
        {
            if (!OidcExchangeService.CommonOidcInfrastructureClaims.Contains(claim.Type) && !string.IsNullOrEmpty(claim.Value))
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

        if (session.IsStepUp)
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

            // Reconcile IAL from OIDC verification claims (e.g. CO's PingOne/Socure).
            // If the IdP says the user completed identity verification, update our DB
            // to match — the IdP is the source of truth for OIDC-based verification.
            // For states without OIDC verification (e.g. DC), Translate() returns null
            // because the IdP claims don't contain the configured verification claim names.
            var verification = verificationClaimTranslator.Translate(additionalClaims);
            if (verification != null)
            {
                ReconcileIalFromOidcVerification(user, verification);
                await userRepository.UpdateUserAsync(user, cancellationToken);

                logger.LogInformation(
                    "OIDC verification claim reconciled: UserId {UserId}, IalLevel {IalLevel}, IsExpired {IsExpired}, VerifiedAt {VerifiedAt}",
                    user.Id, user.IalLevel, verification.IsExpired, verification.VerifiedAt);
            }
        }

        var token = jwtService.GenerateToken(user, additionalClaims);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwtSettingsOptions.Value.ExpirationMinutes);
        AuthCookies.SetAuthCookie(Response, token, expiresAt);

        string? safeReturnUrl = null;
        if (session.IsStepUp)
        {
            safeReturnUrl = TrySanitizeStepUpReturnUrl(body.ReturnUrl);
            if (safeReturnUrl == null && !string.IsNullOrWhiteSpace(body.ReturnUrl))
                logger.LogWarning("Step-up complete-login: returnUrl rejected (must be a safe relative path).");
        }
        return Ok(new CompleteLoginResponse(ReturnUrl: safeReturnUrl));
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
    /// Updates a user's IAL and proofing fields based on translated OIDC verification claims.
    /// If the verification is expired, resets to IAL1 (the user must re-verify).
    /// If valid, promotes to the verified IAL level.
    /// </summary>
    private static void ReconcileIalFromOidcVerification(User user, OidcVerificationResult verification)
    {
        if (verification.IsExpired)
        {
            // Verification has lapsed — reset to baseline IAL1 (they completed OIDC login,
            // so they're at least IAL1, but no longer IAL1+).
            user.IalLevel = UserIalLevel.IAL1;
            user.IdProofingStatus = IdProofingStatus.Expired;
            return;
        }

        // Valid, non-expired verification from the IdP — update to match.
        user.IalLevel = verification.IalLevel;
        user.IdProofingStatus = IdProofingStatus.Completed;
        user.IdProofingCompletedAt = verification.VerifiedAt;
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
