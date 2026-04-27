using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.Api.Controllers.Auth;

/// <summary>
/// Controller for handling authorization status checks, token refresh, and logout.
/// The portal JWT is stored in an HttpOnly cookie (see <c>AuthCookies</c>); endpoints
/// that issue a new session write to the cookie and return non-sensitive metadata only.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(
    ILogger<AuthController> logger,
    IOptions<JwtSettings> jwtSettingsOptions) : ControllerBase
{
    /// <summary>
    /// Returns the authenticated user's session info read from validated JWT claims.
    /// The JWT is carried in the HttpOnly session cookie; this endpoint exposes the
    /// non-sensitive claims the SPA needs for IAL gating, analytics, and UI state.
    /// </summary>
    /// <returns>An OK result with the session info for the authenticated caller.</returns>
    /// <response code="200">Caller is authenticated. Returns session info.</response>
    /// <response code="401">Caller is not authenticated (missing or invalid session cookie).</response>
    [HttpGet("status")]
    [Authorize]
    [ProducesResponseType(typeof(AuthorizationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetAuthorizationStatus()
    {
        var userId = User.GetUserId();

        logger.LogInformation("Authorization status check successful for UserId {UserId}, Phone={MaskedPhone}",
            userId?.ToString() ?? "unknown", GetMaskedPhone());

        return Ok(new AuthorizationStatusResponse(
            IsAuthorized: true,
            Email: User.GetUserEmail(),
            Ial: User.FindFirst(JwtClaimTypes.Ial)?.Value,
            IdProofingStatus: int.TryParse(User.FindFirst(JwtClaimTypes.IdProofingStatus)?.Value, out var s) ? s : null,
            IdProofingCompletedAt: long.TryParse(User.FindFirst(JwtClaimTypes.IdProofingCompletedAt)?.Value, out var c) ? c : null,
            IdProofingExpiresAt: long.TryParse(User.FindFirst(JwtClaimTypes.IdProofingExpiresAt)?.Value, out var e) ? e : null,
            IsCoLoaded: bool.TryParse(User.FindFirst(JwtClaimTypes.IsCoLoaded)?.Value, out var cl) ? cl : null));
    }

    /// <summary>
    /// Clears the local session cookie and redirects to the IdP's end_session_endpoint
    /// (RP-Initiated Logout) when OIDC is configured, or to <c>/login</c> otherwise.
    /// The entire redirect chain is browser-level — no JavaScript processes the IdP
    /// logout URL, eliminating XSS as a vector for redirect tampering.
    /// </summary>
    [HttpGet("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public async Task<IActionResult> Logout(
        [FromServices] IConfiguration config,
        [FromServices] IOidcExchangeService oidcExchangeService,
        CancellationToken cancellationToken = default)
    {
        AuthCookies.ClearAuthCookie(Response);

        var discoveryEndpoint = config["Oidc:DiscoveryEndpoint"];
        var clientId = config["Oidc:ClientId"];
        var callbackRedirectUri = config["Oidc:CallbackRedirectUri"];

        if (!string.IsNullOrEmpty(discoveryEndpoint) && !string.IsNullOrEmpty(clientId)
            && !string.IsNullOrEmpty(callbackRedirectUri))
        {
            try
            {
                var oidcConfig = await oidcExchangeService.GetDiscoveryConfigAsync(
                    isStepUp: false, cancellationToken);

                if (!string.IsNullOrEmpty(oidcConfig.EndSessionEndpoint))
                {
                    var origin = new Uri(callbackRedirectUri).GetLeftPart(UriPartial.Authority);
                    var postLogoutUri = $"{origin}/login";
                    var logoutUrl = $"{oidcConfig.EndSessionEndpoint}" +
                        $"?client_id={Uri.EscapeDataString(clientId)}" +
                        $"&post_logout_redirect_uri={Uri.EscapeDataString(postLogoutUri)}";

                    logger.LogInformation(
                        "Logout: redirecting to IdP end_session_endpoint (reason=oidc_logout)");
                    return Redirect(logoutUrl);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Logout: failed to fetch OIDC discovery document, falling back to /login (reason=discovery_failed)");
            }
        }

        return Redirect("/login");
    }

    /// <summary>
    /// Refreshes the session JWT for the authenticated user. The new token is returned via
    /// the HttpOnly session cookie; the response body carries no token.
    /// </summary>
    /// <param name="handler">The command handler for processing the token refresh.</param>
    /// <returns>204 No Content on success; the new session cookie is set on the response.</returns>
    /// <response code="204">Token refreshed successfully. Cookie updated; no body.</response>
    /// <response code="400">Invalid request or validation error.</response>
    /// <response code="401">User is not authorized (missing or invalid session cookie).</response>
    /// <response code="404">User not found.</response>
    /// <response code="500">An error occurred while refreshing the token.</response>
    [HttpPost("refresh")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> RefreshToken(
        [FromServices] ICommandHandler<RefreshTokenCommand, string> handler)
    {
        var userId = User.GetUserId();

        if (userId == null)
        {
            logger.LogWarning("Token refresh attempted but user ID could not be extracted from claims");
            return Unauthorized(new ErrorResponse("Unable to identify user from token."));
        }

        logger.LogInformation("Token refresh request received for UserId {UserId}, Phone={MaskedPhone}",
            userId, GetMaskedPhone());

        var command = new RefreshTokenCommand
        {
            CurrentPrincipal = User
        };
        var result = await handler.Handle(command);

        if (result.IsSuccess)
        {
            logger.LogInformation("Token refreshed successfully for UserId {UserId}, Phone={MaskedPhone}",
                userId, GetMaskedPhone());
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwtSettingsOptions.Value.ExpirationMinutes);
            AuthCookies.SetAuthCookie(Response, result.Value, expiresAt);
            return NoContent();
        }
        else
        {
            logger.LogWarning("Token refresh failed for UserId {UserId}: {Message}", userId, result.Message);

            if (result is ValidationFailedResult<string> validationFailed)
            {
                return BadRequest(new ErrorResponse(result.Message, validationFailed.Errors));
            }

            if (result is PreconditionFailedResult<string> preconditionFailed)
            {
                // Return 404 for NotFound, 409 for Conflict, etc.
                return preconditionFailed.Reason == PreconditionFailedReason.NotFound
                    ? NotFound(new ErrorResponse(result.Message))
                    : BadRequest(new ErrorResponse(result.Message));
            }

            return BadRequest(new ErrorResponse(result.Message));
        }
    }

    /// <summary>
    /// Extracts and masks the phone number from the authenticated user's JWT claims.
    /// Returns a masked value like "***-***-1234", or null if no phone claim is present.
    /// </summary>
    private string? GetMaskedPhone()
    {
        var phone = User.FindFirst("phone")?.Value ?? User.FindFirst("phone_number")?.Value;
        return PiiMasker.MaskPhone(phone);
    }
}
