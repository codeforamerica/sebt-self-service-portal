using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
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
        var email = GetUserEmail();

        logger.LogInformation("Authorization status check successful for user {Email}", email ?? "unknown");

        return Ok(new AuthorizationStatusResponse(
            IsAuthorized: true,
            Email: email,
            Ial: User.FindFirst(JwtClaimTypes.Ial)?.Value,
            IdProofingStatus: int.TryParse(User.FindFirst(JwtClaimTypes.IdProofingStatus)?.Value, out var s) ? s : null,
            IdProofingCompletedAt: long.TryParse(User.FindFirst(JwtClaimTypes.IdProofingCompletedAt)?.Value, out var c) ? c : null,
            IdProofingExpiresAt: long.TryParse(User.FindFirst(JwtClaimTypes.IdProofingExpiresAt)?.Value, out var e) ? e : null));
    }

    /// <summary>
    /// Clears the HttpOnly session cookie. Safe to call even when no session exists —
    /// the browser will simply receive a delete instruction for a cookie it does not have.
    /// </summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        AuthCookies.ClearAuthCookie(Response);
        return NoContent();
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
        var email = GetUserEmail();

        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("Token refresh attempted but email could not be extracted from claims");
            return Unauthorized(new ErrorResponse("Unable to identify user from token."));
        }

        logger.LogInformation("Token refresh request received for email {Email}", email);

        var command = new RefreshTokenCommand { Email = email, CurrentPrincipal = User };
        var result = await handler.Handle(command);

        if (result.IsSuccess)
        {
            logger.LogInformation("Token refreshed successfully for email {Email}", email);
            var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwtSettingsOptions.Value.ExpirationMinutes);
            AuthCookies.SetAuthCookie(Response, result.Value, expiresAt);
            return NoContent();
        }
        else
        {
            logger.LogWarning("Token refresh failed for email {Email}: {Message}", email, result.Message);

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
    /// Extracts the user's email address from the authenticated user's claims.
    /// Tries both long (.NET) and short (JWT) claim names so it works whether or not
    /// the JWT handler has mapped inbound claims.
    /// </summary>
    /// <returns>The user's email address, or null if not found.</returns>
    private string? GetUserEmail()
    {
        return User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("email")?.Value
            ?? User.FindFirst("sub")?.Value
            ?? User.Identity?.Name;
    }
}

