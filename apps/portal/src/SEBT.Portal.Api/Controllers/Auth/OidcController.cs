using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth.CompleteOidcLogin;
using SEBT.Portal.UseCases.Auth.OidcCallback;

namespace SEBT.Portal.Api.Controllers.Auth;

/// <summary>
/// OIDC endpoints for external IdP login and step-up. Primary config uses flat <c>Oidc</c> keys
/// (<c>DiscoveryEndpoint</c>, <c>ClientId</c>, <c>CallbackRedirectUri</c>); optional <c>Oidc:StepUp:*</c>
/// selects a second client for elevated verification when <c>stepUp=true</c> on the config endpoint.
///
/// The controller owns PKCE generation, cookies, and HTTP mapping; the callback and
/// complete-login business orchestration lives in UseCases handlers.
/// </summary>
[ApiController]
[Route("api/auth/oidc")]
public class OidcController(
    IConfiguration config,
    ILogger<OidcController> logger,
    IOidcCallbackFailureLogger callbackFailureLogger,
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

        var clientId = stepUp ? config["Oidc:StepUp:ClientId"] : config["Oidc:ClientId"];
        var redirectUri = stepUp
            ? (config["Oidc:StepUp:RedirectUri"] ?? config["Oidc:CallbackRedirectUri"])
            : config["Oidc:CallbackRedirectUri"];
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

        var clientId = stepUp ? config["Oidc:StepUp:ClientId"] : config["Oidc:ClientId"];
        var redirectUri = stepUp
            ? (config["Oidc:StepUp:RedirectUri"] ?? config["Oidc:CallbackRedirectUri"])
            : config["Oidc:CallbackRedirectUri"];
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(redirectUri))
        {
            logger.LogError(
                "OIDC config missing for stateCode {StateCode} (reason=oidc_not_configured)",
                stateCode);
            return Redirect("/login");
        }

        // Fetch the authorization endpoint from the cached discovery document.
        OidcDiscoveryInfo oidcConfig;
        try
        {
            oidcConfig = await exchangeService.GetDiscoveryInfoAsync(stepUp, cancellationToken);
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
        // falling back to the configured default.
        var languageParam = language ?? config["Oidc:LanguageParam"] ?? "en";
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
    /// locate the pre-auth session; the orchestration (state validation, replay
    /// protection, code exchange, session advancement) is dispatched to
    /// <see cref="OidcCallbackCommandHandler"/>. The request body only needs
    /// <c>code</c> and <c>state</c>.
    /// </summary>
    [HttpPost("callback")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Callback(
        [FromBody] OidcCallbackRequest? body,
        [FromServices] ICommandHandler<OidcCallbackCommand, OidcCallbackResponse> handler,
        CancellationToken cancellationToken)
    {
        if (body == null || string.IsNullOrEmpty(body.Code))
            return BadRequest(new ErrorResponse("Missing code."));

        var result = await handler.Handle(new OidcCallbackCommand
        {
            Code = body.Code,
            State = body.State,
            SessionId = OidcSessionCookie.Read(Request)
        }, cancellationToken);

        if (result.IsSuccess)
        {
            return Ok(new { callbackToken = result.Value.CallbackToken });
        }

        return MapFailure(result);
    }

    /// <summary>
    /// Completes OIDC login. Requires the <c>oidc_session</c> cookie to locate the
    /// pre-auth session; the orchestration (callback-token verification, user
    /// resolution, portal JWT minting) is dispatched to
    /// <see cref="CompleteOidcLoginCommandHandler"/>. On success the portal JWT is
    /// written to the session cookie and the pre-auth cookie is cleared. The request
    /// body only needs <c>callbackToken</c>.
    /// </summary>
    [HttpPost("complete-login")]
    [ProducesResponseType(typeof(CompleteLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CompleteLogin(
        [FromBody] CompleteLoginRequest body,
        [FromServices] ICommandHandler<CompleteOidcLoginCommand, CompleteOidcLoginResponse> handler,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(body.CallbackToken))
            return BadRequest(new ErrorResponse("Missing callbackToken."));

        var result = await handler.Handle(new CompleteOidcLoginCommand
        {
            CallbackToken = body.CallbackToken,
            SessionId = OidcSessionCookie.Read(Request)
        }, cancellationToken);

        // The handler consumes the session on its first advance; any non-Forbidden outcome
        // means it was consumed, so clear the pre-auth cookie (Forbidden = the session was
        // never validated, keep the cookie for a retry with the same browser state).
        if (result is not ForbiddenResult<CompleteOidcLoginResponse>)
        {
            OidcSessionCookie.Clear(Response);
        }

        if (result.IsSuccess)
        {
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwtSettingsOptions.Value.ExpirationMinutes);
            AuthCookies.SetAuthCookie(Response, result.Value.Token, expiresAt);
            return Ok(new CompleteLoginResponse(ReturnUrl: result.Value.ReturnUrl));
        }

        // Not-configured carries a dev-only remediation hint; every other failure uses the
        // shared mapping.
        if (result is DependencyFailedResult<CompleteOidcLoginResponse> { Reason: DependencyFailedReason.NotConfigured } notConfigured)
        {
            var hint = environment.IsDevelopment() ? "Set Oidc:CompleteLoginSigningKey in appsettings." : "";
            return StatusCode(OidcResultHttpStatus.For(result), new { error = notConfigured.Message, hint });
        }

        return MapFailure(result);
    }

    /// <summary>
    /// Maps a failed handler result to the HTTP contract these endpoints have always
    /// returned. The status code comes from <see cref="OidcResultHttpStatus"/> — the
    /// same table the handlers record on their failure-log entries — so the logged
    /// and returned statuses cannot drift apart.
    /// </summary>
    private ObjectResult MapFailure<T>(Result<T> result)
    {
        var error = result is ValidationFailedResult<T> validation
            ? validation.Errors.FirstOrDefault()?.Message ?? validation.Message
            : result.Message;
        return StatusCode(OidcResultHttpStatus.For(result), new ErrorResponse(error));
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

}
