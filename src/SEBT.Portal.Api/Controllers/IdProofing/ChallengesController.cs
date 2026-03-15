using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Api.Controllers.IdProofing;

/// <summary>
/// Controller for document verification challenge operations.
/// </summary>
[ApiController]
[Route("api/challenges")]
[Authorize]
public class ChallengesController(ILogger<ChallengesController> logger) : ControllerBase
{
    /// <summary>
    /// Starts a document verification challenge by generating a Socure DocV session token.
    /// Called when the user clicks "Continue" on the document verification interstitial.
    /// </summary>
    /// <param name="id">The challenge's public GUID.</param>
    /// <param name="handler">The command handler.</param>
    /// <param name="userRepository">User repository for resolving user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">DocV session created. Returns token and URL for the frontend SDK.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Challenge not found or belongs to a different user.</response>
    /// <response code="409">Challenge is not in a state that allows starting.</response>
    [HttpGet("{id:guid}/start")]
    [ProducesResponseType(typeof(StartChallengeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(
        Guid id,
        [FromServices] ICommandHandler<StartChallengeCommand, StartChallengeResponse> handler,
        [FromServices] IUserRepository userRepository,
        CancellationToken cancellationToken)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("Challenge start request but email could not be extracted from claims");
            return Unauthorized(new ErrorResponse("Unable to identify user from token."));
        }

        var user = await userRepository.GetUserByEmailAsync(email, cancellationToken);
        if (user == null)
        {
            logger.LogWarning("Challenge start request but authenticated user not found in database");
            return Unauthorized(new ErrorResponse("Unable to identify user from token."));
        }

        var command = new StartChallengeCommand
        {
            ChallengeId = id,
            UserId = user.Id
        };

        var result = await handler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }
}
