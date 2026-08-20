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
using static SEBT.Portal.Core.Utilities.PiiMasker;

namespace SEBT.Portal.Api.Controllers.Auth;

/// <summary>
/// OIDC endpoints for external IdP login and step-up. Primary config uses flat <c>Oidc</c> keys
/// (<c>DiscoveryEndpoint</c>, <c>ClientId</c>, <c>CallbackRedirectUri</c>); optional <c>Oidc:StepUp:*</c>
/// selects a second client for elevated verification when <c>stepUp=true</c> on the config endpoint.
/// </summary>
[ApiController]
[Route("api/auth/oidc")]
public class OidcController(
    IOptionsSnapshot<OidcSettings> oidcSettings,
    IOptionsSnapshot<OidcStepUpSettings> oidcStepUpSettings,
    ILogger<OidcController> logger,
    IOidcCallbackFailureLogger callbackFailureLogger,
    IUserRepository userRepository,
    IOidcTokenService oidcTokenService,
    IOptions<JwtSettings> jwtSettingsOptions,
    IStateAllowlist stateAllowlist,
    IPreAuthSessionStore sessionStore,
    IWebHostEnvironment environment) : ControllerBase
{
    /// <summary>
    /// OIDC config + pre-auth session creation. Generates PKCE server-side, stores
    /// <c>state</c> + <c>code_verifier</c> + <c>stateCode</c> in the session store, sets an
    /// <c>oidc_session</c> HttpOnly cookie, and returns only the <c>code_challenge</c> +
    /// <c>state</c> to the browser. The <c>code_verifier</c> never leaves the server.
    /// </summary>
    /// <remarks>
    /// The authorization endpoint is intentionally NOT returned in this response.
    /// Use the <c>GET {code}/authorize</c> endpoint for server-side redirect instead.
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

        var clientId = GetOidcSettings(stepUp).ClientId;
        var redirectUri = stepUp
            ? GetOidcSettings(stepUp).RedirectUri ?? oidcSettings.Value.CallbackRedirectUri
            : oidcSettings.Value.CallbackRedirectUri;
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            logger.LogError(
                "OIDC config missing for stateCode {StateCode} (reason=oidc_not_configured)",
                stateCode);
            var hint = environment.IsDevelopment()
                ? "Set Oidc:ClientId and Oidc:CallbackRedirectUri in appsettings."
                : "";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "OIDC not configured.", hint });
        }

        // Generate PKCE server-side — code_verifier never leaves the server.
        var codeVerifier = PkceHelper.GenerateCodeVerifier();
        var codeChallenge = PkceHelper.ComputeCodeChallenge(codeVerifier);
        var state = PkceHelper.GenerateState();

        // Create the pre-auth session and set the cookie.
        var session = await sessionStore.CreateAsync(
            stateCode, state, codeVerifier, redirectUri, stepUp,
            returnUrl: null, cancellationToken);
        OidcSessionCookie.Set(Response, session.Id);

        logger.LogInformation(
            "OIDC GetConfig succeeded: StateCode={StateCode}, IsStepUp={IsStepUp}, SessionId={SessionId}",
            stateCode, stepUp, session.Id);

        return Ok(new
        {
            clientId,
            redirectUri,
            state,
            codeChallenge,
            codeChallengeMethod = "S256"
        });
    }

    /// <summary>
    /// Server-side OIDC authorize redirect. Builds the full authorization URL on the server
    /// using the <c>authorization_endpoint</c> from the IdP discovery document and returns a
    /// 302 redirect.
    /// </summary>
    [HttpGet("{code}/authorize")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Authorize(
        [FromRoute] string code,
        [FromQuery] bool stepUp = false,
        [FromQuery] string? returnUrl = null,
        [FromQuery] string? language = null,
        [FromServices] IOidcExchangeService exchangeService = null!,
        CancellationToken cancellationToken = default)
    {
        var stateCode = stateAllowlist.TryResolve(code);
        if (stateCode == null)
        {
            logger.LogWarning("OIDC Authorize rejected: unknown stateCode (reason=unknown_state)");
            return BadRequest(new ErrorResponse("Unknown or unsupported stateCode."));
        }

        // Sanitize returnUrl for step-up flows; ignore for normal login.
        string? safeReturnUrl = null;
        if (stepUp && !string.IsNullOrWhiteSpace(returnUrl))
        {
            safeReturnUrl = TrySanitizeStepUpReturnUrl(returnUrl);
            if (safeReturnUrl == null)
            {
                logger.LogWarning("OIDC Authorize: returnUrl rejected (must be a safe relative path).");
            }
        }

        // Skip the IdP round-trip (and the cost of another Socure call) when the caller
        // is already step-up complete. Catches browser-back navigation that lands back
        // on this endpoint after a successful step-up.
        if (stepUp && HasFreshIal1Plus(HttpContext.User))
        {
            logger.LogInformation(
                "OIDC Authorize: step-up short-circuited (reason=already_ial1plus, StateCode={StateCode})",
                stateCode);
            return LocalRedirect(safeReturnUrl ?? "/dashboard");
        }

        var clientId = GetOidcSettings(stepUp).ClientId;
        var redirectUri = GetOidcSettings(stepUp).RedirectUri ?? oidcSettings.Value.CallbackRedirectUri;

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            logger.LogError(
                "OIDC config missing for stateCode {StateCode} (reason=oidc_not_configured)",
                stateCode);
            return Redirect("/login");
        }

        // Fetch the authorization endpoint from the cached discovery document.
        Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration oidcConfig;
        try
        {
            oidcConfig = await exchangeService.GetDiscoveryConfigAsync(stepUp, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "OIDC Authorize: failed to fetch discovery document (reason=discovery_failed)");
            return Redirect("/login");
        }

        if (string.IsNullOrEmpty(oidcConfig.AuthorizationEndpoint))
        {
            logger.LogError("OIDC Authorize: discovery document missing authorization_endpoint");
            return Redirect("/login");
        }

        // Generate PKCE server-side
        var codeVerifier = PkceHelper.GenerateCodeVerifier();
        var codeChallenge = PkceHelper.ComputeCodeChallenge(codeVerifier);
        var state = PkceHelper.GenerateState();

        // Create the pre-auth session and set the cookie.
        var session = await sessionStore.CreateAsync(
            stateCode, state, codeVerifier, redirectUri, stepUp,
            safeReturnUrl, cancellationToken);
        OidcSessionCookie.Set(Response, session.Id);

        // Build the authorization URL server-side (mirrors the frontend's buildAuthorizationUrl).
        // Use the language from the query param (set by the frontend based on user choice),
        // falling back to the default.
        var languageParam = language ?? "en";
        var authUrl = BuildAuthorizationUrl(
            oidcConfig.AuthorizationEndpoint, clientId, redirectUri,
            state, codeChallenge, languageParam);

        return Redirect(authUrl);
    }

    /// <summary>
    /// Builds the full OIDC authorization URL with all required query parameters.
    /// </summary>
    private static string BuildAuthorizationUrl(
        string authorizationEndpoint,
        string clientId,
        string redirectUri,
        string state,
        string codeChallenge,
        string languageParam)
    {
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = "openid email profile phone",
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["prompt"] = "login",
            ["max_age"] = "0"
        };
        if (!string.IsNullOrEmpty(languageParam))
        {
            query["language"] = languageParam;
        }

        var queryString = string.Join("&",
            query.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
        return $"{authorizationEndpoint}?{queryString}";
    }

    /// <summary>
    /// Records browser-only OIDC callback failures (IdP redirect <c>?error=</c> or missing
    /// <c>code</c>/<c>state</c>) before redirect to off-boarding. Failures from
    /// <c>POST callback</c> / <c>complete-login</c> are logged server-side only.
    /// </summary>
    [HttpPost("report-failure")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReportCallbackFailure(
        [FromBody] OidcCallbackFailureReportRequest? body,
        CancellationToken cancellationToken)
    {
        if (body == null
            || string.IsNullOrWhiteSpace(body.Reason)
            || !callbackFailureLogger.IsAllowedClientReason(body.Reason))
        {
            return BadRequest(new ErrorResponse("Invalid or missing reason."));
        }

        var sessionId = OidcSessionCookie.Read(Request);
        bool? isStepUp = null;
        if (!string.IsNullOrEmpty(sessionId))
        {
            var session = await sessionStore.GetAsync(sessionId, cancellationToken);
            isStepUp = session?.IsStepUp;
        }

        callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
        {
            Reason = body.Reason,
            IdpError = body.IdpError,
            IdpErrorDescription = body.IdpErrorDescription,
            HttpStatus = body.HttpStatus,
            ApiError = body.ApiError,
            SessionId = sessionId,
            IsStepUp = isStepUp,
            Phase = body.Phase,
            HasCode = body.HasCode,
            HasState = body.HasState
        });

        return NoContent();
    }

    /// <summary>
    /// Server-side OIDC callback. Requires the <c>oidc_session</c> cookie to
    /// locate the pre-auth session. Validates <c>state</c> against the stored value,
    /// uses the stored <c>code_verifier</c> for the token exchange (never from the
    /// request body), and advances the session to <c>CallbackCompleted</c>.
    /// The <c>stateCode</c> and <c>isStepUp</c> values are read from the session —
    /// the request body only needs <c>code</c> and <c>state</c>.
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
        if (body == null || string.IsNullOrEmpty(body.Code))
            return BadRequest(new ErrorResponse("Missing code."));

        // --- Require the oidc_session cookie ---
        var sessionId = OidcSessionCookie.Read(Request);
        if (string.IsNullOrEmpty(sessionId))
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_session",
                Phase = "callback",
                HttpStatus = StatusCodes.Status403Forbidden
            });
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Missing pre-auth session."));
        }

        var session = await sessionStore.GetAsync(sessionId, cancellationToken);
        if (session == null)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_session",
                Phase = "callback",
                SessionId = sessionId,
                HttpStatus = StatusCodes.Status403Forbidden
            });
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Pre-auth session expired or invalid."));
        }

        // --- Validate state matches stored value (CSRF protection) ---
        if (string.IsNullOrEmpty(body.State) || body.State != session.State)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "mismatched_state",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusCodes.Status400BadRequest
            });
            return BadRequest(new ErrorResponse("State parameter mismatch."));
        }

        // --- Verify the session hasn't already been used (fail fast before the exchange) ---
        if (session.Phase != PreAuthSessionPhase.Created)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "replay",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusCodes.Status400BadRequest,
                ApiError = $"Phase={session.Phase}"
            });
            return BadRequest(new ErrorResponse("Pre-auth session has already been used."));
        }

        // --- Exchange the authorization code from PingOne (body.Code) using server-side
        // session values. code_verifier, redirectUri, and isStepUp are read from the
        // pre-auth session — never from the body. ---
        var result = await exchangeService.ExchangeCodeAsync(
            body.Code,
            session.CodeVerifier,
            session.RedirectUri,
            session.IsStepUp,
            sessionId,
            cancellationToken);

        if (!result.Success)
        {
            // Exchange failures are logged in <see cref="OidcExchangeService"/> with SessionId and IdP detail.
            return StatusCode(result.StatusCode, new ErrorResponse(result.Error ?? "Exchange failed."));
        }

        // --- Advance session to CallbackCompleted and store the callback token hash ---
        var tokenHash = IPreAuthSessionStore.HashCallbackToken(result.CallbackToken!);
        var advanced = await sessionStore.TryAdvanceToCallbackCompletedAsync(sessionId, tokenHash, cancellationToken);
        if (!advanced)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "replay",
                Phase = "callback",
                SessionId = sessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusCodes.Status400BadRequest
            });
            return BadRequest(new ErrorResponse("Pre-auth session has already been used."));
        }

        logger.LogInformation(
            "OIDC Callback exchange succeeded: IsStepUp={IsStepUp}, Phone={MaskedPhone}, SessionId={SessionId}",
            session.IsStepUp,
            MaskPhone(result.PhoneClaim),
            sessionId);

        return Ok(new { callbackToken = result.CallbackToken });
    }

    /// <summary>
    /// Completes OIDC login. Requires the <c>oidc_session</c> cookie to locate
    /// the pre-auth session. Verifies the callback token was issued for this session and
    /// has not been used before. On success, mints the portal JWT, marks the session
    /// consumed, and clears the pre-auth cookie. The <c>stateCode</c>, <c>isStepUp</c>,
    /// and <c>returnUrl</c> are read from the session — the request body only needs
    /// <c>callbackToken</c>.
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
        // Bind callbackToken after null check; the token is validated cryptographically
        // (signature + hash match) before any sensitive action.
        if (string.IsNullOrEmpty(body.CallbackToken))
            return BadRequest(new ErrorResponse("Missing callbackToken."));
        var callbackToken = body.CallbackToken;

        // --- Require the oidc_session cookie ---
        var sessionId = OidcSessionCookie.Read(Request);
        if (string.IsNullOrEmpty(sessionId))
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_session",
                Phase = "complete-login",
                HttpStatus = StatusCodes.Status403Forbidden
            });
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Missing pre-auth session."));
        }

        // --- Retrieve session (stateCode, isStepUp, returnUrl are authoritative from here) ---
        var session = await sessionStore.GetAsync(sessionId, cancellationToken);
        if (session == null)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_session",
                Phase = "complete-login",
                SessionId = sessionId,
                HttpStatus = StatusCodes.Status403Forbidden
            });
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Pre-auth session invalid, expired, or already used."));
        }

        // --- Verify the callback token matches this session and hasn't been consumed ---
        var tokenHash = IPreAuthSessionStore.HashCallbackToken(callbackToken);
        var advanced = await sessionStore.TryAdvanceToLoginCompletedAsync(sessionId, tokenHash, cancellationToken);
        if (!advanced)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "replay",
                Phase = "complete-login",
                SessionId = sessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusCodes.Status403Forbidden
            });
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Pre-auth session invalid, expired, or already used."));
        }

        // Clear the pre-auth cookie and remove the session from cache (defense-in-depth:
        // even if the phase check were bypassed, the code_verifier is gone from memory).
        OidcSessionCookie.Clear(Response);
        await sessionStore.RemoveAsync(sessionId, cancellationToken);

        var signingKey = oidcSettings.Value.CompleteLoginSigningKey;
        if (string.IsNullOrEmpty(signingKey))
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "complete_login_not_configured",
                Phase = "complete-login",
                SessionId = sessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusCodes.Status503ServiceUnavailable
            });
            var hint = environment.IsDevelopment() ? "Set Oidc:CompleteLoginSigningKey in appsettings." : "";
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "Complete-login not configured.", hint });
        }

        var portalOrigin = oidcSettings.Value.PortalOrigin;
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
            IssuerSigningKeyResolver = (_, _, _, _) => [key]
        };
        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false // Preserve original JWT claim names (sub, email)
        };
        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(callbackToken, validationParams, out _);
        }
        catch (Exception ex)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "invalid_callback_token",
                Phase = "complete-login",
                SessionId = sessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusCodes.Status400BadRequest,
                ApiError = OidcLogSanitizer.Sanitize(ex.Message)
            });
            logger.LogError(ex, "OIDC complete-login off-boarding: invalid_callback_token (SessionId={SessionId})", sessionId);
            return BadRequest(new ErrorResponse("Invalid or expired callback token."));
        }

        // Extract sub + email from principal for user lookup. The service handles
        // all claim processing (filtering, verification, IAL derivation).
        var subClaim = principal.FindFirst("sub")?.Value;
        var email = GetEmailFromClaims(principal);
        var phoneClaim = principal.FindFirst("phone")?.Value;
        var maskedPhone = MaskPhone(phoneClaim);

        if (phoneClaim == null)
        {
            logger.LogError("OIDC incoming claims missing 'phone' (SessionId={SessionId})", sessionId);
        }

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(subClaim))
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "missing_identity_claim",
                Phase = "complete-login",
                SessionId = sessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusCodes.Status400BadRequest
            });
            return BadRequest(new ErrorResponse("Callback token must contain an email or sub claim."));
        }

        User user;

        if (session.IsStepUp)
        {
            if (string.IsNullOrWhiteSpace(subClaim))
            {
                callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
                {
                    Reason = "missing_sub_claim",
                    Phase = "complete-login",
                    SessionId = sessionId,
                    IsStepUp = true,
                    HttpStatus = StatusCodes.Status400BadRequest
                });
                return BadRequest(new ErrorResponse("Callback token must contain a sub claim."));
            }

            var existingEntity = await userRepository.GetUserByExternalIdAsync(subClaim, cancellationToken);
            if (existingEntity == null)
            {
                callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
                {
                    Reason = "step_up_user_not_found",
                    Phase = "complete-login",
                    SessionId = sessionId,
                    IsStepUp = true,
                    HttpStatus = StatusCodes.Status400BadRequest
                });
                return BadRequest(new { error = "Step-up requires an existing session. Please sign in again." });
            }

            user = existingEntity;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(subClaim))
            {
                callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
                {
                    Reason = "missing_sub_claim",
                    Phase = "complete-login",
                    SessionId = sessionId,
                    IsStepUp = false,
                    HttpStatus = StatusCodes.Status400BadRequest
                });
                return BadRequest(new ErrorResponse("Callback token must contain a sub claim."));
            }

            // Pass email from IdP claims as a migration hint: if no user exists for
            // this sub but one exists for this email, adopt that legacy record.
            // TODO: Remove email parameter once all existing users have been migrated.
            var emailHint = principal.FindFirst("email")?.Value;
            var (createdUser, _) = await userRepository.GetOrCreateUserByExternalIdAsync(
                subClaim, emailHint, cancellationToken);
            user = createdUser;
        }

        // The service handles claim filtering, verification translation,
        // IAL derivation, and timestamp computation.
        var tokenResult = oidcTokenService.GenerateForOidcLogin(user, principal, session.IsStepUp);

        if (!tokenResult.IsSuccess)
        {
            callbackFailureLogger.Log(new OidcCallbackFailureLogEntry
            {
                Reason = "token_generation_failed",
                Phase = "complete-login",
                SessionId = sessionId,
                IsStepUp = session.IsStepUp,
                HttpStatus = StatusCodes.Status400BadRequest,
                ApiError = tokenResult.Message
            });
            return BadRequest(new { error = "Step-up verification failed. Please try again." });
        }

        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwtSettingsOptions.Value.ExpirationMinutes);
        AuthCookies.SetAuthCookie(Response, tokenResult.Value, expiresAt);

        logger.LogInformation(
            "OIDC {FlowType} complete: UserId {UserId}, Phone={MaskedPhone}, SessionId={SessionId}",
            session.IsStepUp ? "step-up" : "login", user.Id, maskedPhone, sessionId);

        var safeReturnUrl = session.IsStepUp ? session.ReturnUrl : null;
        return Ok(new CompleteLoginResponse(ReturnUrl: safeReturnUrl));
    }

    private const int MaxStepUpReturnUrlLength = 4096;

    /// <summary>
    /// Mirrors the frontend's <c>hasIal1Plus(session) &amp;&amp; isIdProofingCompletionFresh(session)</c>:
    /// the portal JWT carries at least <see cref="UserIalLevel.IAL1plus"/> and an unexpired
    /// <c>id_proofing_expires_at</c> Unix-seconds claim.
    /// </summary>
    private static bool HasFreshIal1Plus(ClaimsPrincipal user)
    {
        if (user.GetIalLevel() < UserIalLevel.IAL1plus)
            return false;

        var expiresAtClaim = user.FindFirst(JwtClaimTypes.IdProofingExpiresAt)?.Value;
        if (!long.TryParse(expiresAtClaim, out var expiresAtUnix))
            return false;

        return DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix) > DateTimeOffset.UtcNow;
    }

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

    private IOidcCoreSettings GetOidcSettings(bool stepUp) =>
        stepUp ? oidcStepUpSettings.Value : oidcSettings.Value;
}
