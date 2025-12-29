using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Api.Models;

namespace SEBT.Portal.Api.Controllers.Auth;

/// <summary>
/// Controller for handling authorization status checks.
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
        var email = User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name;

        logger.LogInformation("Authorization status check successful for user {Email}", email ?? "unknown");

        return Ok(new AuthorizationStatusResponse(IsAuthorized: true, Email: email));
    }
}

