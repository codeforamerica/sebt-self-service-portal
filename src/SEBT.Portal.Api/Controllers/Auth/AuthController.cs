using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Auth;

namespace SEBT.Portal.Api.Controllers.Auth;

/// <summary>
/// Controller for handling authorization status checks and token refresh.
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController(ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>
    /// Checks whether the current user is authorized (authenticated with a valid JWT token).
    /// This endpoint requires a valid JWT token in the Authorization header.
    /// </summary>
    /// <returns>An OK result with authorization status and user email.</returns>
    /// <response code="200">User is authorized. Returns authorization status and email.</response>
    /// <response code="401">User is not authorized (missing or invalid JWT token).</response>
    [HttpGet("status")]
    [Authorize]
    [ProducesResponseType(typeof(AuthorizationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetAuthorizationStatus()
    {
        // If we reach this point, the user is authenticated (due to [Authorize] attribute)
        var email = GetUserEmail();

        logger.LogInformation("Authorization status check successful for user {Email}", email ?? "unknown");

        return Ok(new AuthorizationStatusResponse(IsAuthorized: true, Email: email));
    }

    /// <summary>
    /// Refreshes the JWT token for the authenticated user, returning a new token with updated user information
    /// including the latest ID proofing status.
    /// </summary>
    /// <param name="handler">The command handler for processing the token refresh.</param>
    /// <returns>An OK result with a new JWT token if refresh is successful; otherwise, a BadRequest or Unauthorized result.</returns>
    /// <response code="200">Token refreshed successfully. Returns a new JWT token.</response>
    /// <response code="400">Invalid request or validation error.</response>
    /// <response code="401">User is not authorized (missing or invalid JWT token).</response>
    /// <response code="404">User not found.</response>
    /// <response code="500">An error occurred while refreshing the token.</response>
    [HttpPost("refresh")]
    [Authorize]
    [ProducesResponseType(typeof(ValidateOtpResponse), StatusCodes.Status200OK)]
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

        var command = new RefreshTokenCommand { Email = email };
        var result = await handler.Handle(command);

        if (result.IsSuccess)
        {
            logger.LogInformation("Token refreshed successfully for email {Email}", email);
            return Ok(new ValidateOtpResponse(result.Value));
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
    /// Attempts to find the email in the following order:
    /// 1. ClaimTypes.Email claim
    /// 2. ClaimTypes.NameIdentifier claim
    /// 3. User.Identity.Name
    /// </summary>
    /// <returns>The user's email address, or null if not found.</returns>
    private string? GetUserEmail()
    {
        return User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name;
    }
}

