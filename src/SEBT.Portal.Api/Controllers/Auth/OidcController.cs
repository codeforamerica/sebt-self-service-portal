using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;

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
    /// Completes OIDC login. Validates the pre-auth session (cookie, state match, phase),
    /// then delegates to <see cref="CompleteOidcLoginCommandHandler"/> for callback token
    /// validation, user creation/update, IAL reconciliation, and JWT generation.
    /// </summary>
    [HttpPost("complete-login")]
    [ProducesResponseType(typeof(CompleteLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CompleteLogin(
        [FromBody] CompleteLoginRequest body,
        [FromServices] ICommandHandler<CompleteOidcLoginCommand, CompleteOidcLoginResult> handler,
        CancellationToken cancellationToken)
    {
        // --- State code validation ---
        var requestedStateCode = stateAllowlist.TryResolve(body.StateCode);
        if (requestedStateCode == null)
            return BadRequest(new ErrorResponse("Missing or unsupported stateCode."));

        if (string.IsNullOrEmpty(body.CallbackToken))
            return BadRequest(new ErrorResponse("Missing callbackToken."));

        // --- Session validation (HTTP infrastructure — stays in controller) ---
        var sessionId = OidcSessionCookie.Read(Request);
        if (string.IsNullOrEmpty(sessionId))
        {
            logger.LogWarning("OIDC CompleteLogin rejected: missing oidc_session cookie (reason=missing_session)");
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Missing pre-auth session."));
        }

        var session = await sessionStore.GetAsync(sessionId, cancellationToken);
        if (session == null)
        {
            logger.LogWarning("OIDC CompleteLogin rejected: session not found (reason=missing_session, SessionId={SessionId})", sessionId);
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Pre-auth session invalid, expired, or already used."));
        }

        if (!string.Equals(requestedStateCode, session.StateCode, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning(
                "OIDC CompleteLogin rejected: stateCode mismatch (reason=mismatched_stateCode, SessionId={SessionId})", sessionId);
            return BadRequest(new ErrorResponse("State code mismatch."));
        }

        var tokenHash = IPreAuthSessionStore.HashCallbackToken(body.CallbackToken);
        var advanced = await sessionStore.TryAdvanceToLoginCompletedAsync(sessionId, tokenHash, cancellationToken);
        if (!advanced)
        {
            logger.LogWarning(
                "OIDC CompleteLogin rejected: session advance failed (reason=replay, SessionId={SessionId})", sessionId);
            return StatusCode(StatusCodes.Status403Forbidden, new ErrorResponse("Pre-auth session invalid, expired, or already used."));
        }

        // Session consumed — clear cookie and remove from store (defense-in-depth)
        OidcSessionCookie.Clear(Response);
        await sessionStore.RemoveAsync(sessionId, cancellationToken);

        // --- Delegate business logic to handler ---
        var command = new CompleteOidcLoginCommand
        {
            CallbackToken = body.CallbackToken,
            IsStepUp = session.IsStepUp,
            ReturnUrl = body.ReturnUrl
        };

        var result = await handler.Handle(command, cancellationToken);

        return result.ToActionResult(
            successMap: r =>
            {
                AuthCookies.SetAuthCookie(Response, r.Token, r.ExpiresAt);
                return Ok(new CompleteLoginResponse(ReturnUrl: r.ReturnUrl));
            },
            failureMap: r => r switch
            {
                _ => BadRequest(new ErrorResponse(result.Message))
            });
    }
}
