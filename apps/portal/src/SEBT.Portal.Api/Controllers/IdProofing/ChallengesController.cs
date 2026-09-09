using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Api.Filters;
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
[ServiceFilter(typeof(ResolveUserFilter))]
public class ChallengesController : ControllerBase
{
    /// <summary>
    /// Starts a document verification challenge by generating a Socure DocV session token.
    /// Called when the user clicks "Continue" on the document verification interstitial.
    /// </summary>
    /// <param name="id">The challenge's public GUID.</param>
    /// <param name="handler">The command handler.</param>
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
        CancellationToken cancellationToken)
    {
        var userId = (Guid)HttpContext.Items[ResolveUserFilter.UserIdKey]!;

        var command = new StartChallengeCommand
        {
            ChallengeId = id,
            UserId = userId
        };

        var result = await handler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Retries document verification after a Socure RESUBMIT decision (DC-301).
    /// Opens a fresh <c>docv_stepup</c> evaluation as a new challenge; the prior Resubmit
    /// challenge stays terminal. Returns the new challenge ID + DocV URL for the user to
    /// re-capture their documents.
    /// </summary>
    /// <param name="id">The prior challenge's public GUID (must be in Resubmit state).</param>
    /// <param name="handler">The command handler.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Fresh challenge created. Returns new challenge ID, token, and URL.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Prior challenge not found or belongs to a different user.</response>
    /// <response code="409">Prior challenge is not in Resubmit state, or step-up returned an unexpected outcome.</response>
    /// <response code="502">Socure step-up call failed.</response>
    [HttpPost("{id:guid}/resubmit")]
    [ProducesResponseType(typeof(ResubmitChallengeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Resubmit(
        Guid id,
        [FromServices] ICommandHandler<ResubmitChallengeCommand, ResubmitChallengeResponse> handler,
        CancellationToken cancellationToken)
    {
        var userId = (Guid)HttpContext.Items[ResolveUserFilter.UserIdKey]!;

        var command = new ResubmitChallengeCommand
        {
            ChallengeId = id,
            UserId = userId
        };

        var result = await handler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }
}
