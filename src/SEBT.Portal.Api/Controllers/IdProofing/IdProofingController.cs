using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Models.IdProofing;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.UseCases.IdProofing;

namespace SEBT.Portal.Api.Controllers.IdProofing;

/// <summary>
/// Controller for ID proofing and document verification flows.
/// All endpoints require authentication.
/// </summary>
[ApiController]
[Route("api/id-proofing")]
[Authorize]
public class IdProofingController(ILogger<IdProofingController> logger) : ControllerBase
{
    /// <summary>
    /// Submits ID proofing data for risk assessment.
    /// Returns whether the user matched, needs document verification, or failed.
    /// </summary>
    /// <response code="200">Assessment completed. Check the result field for the outcome.</response>
    /// <response code="400">Validation error in request data.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">User not found.</response>
    [HttpPost]
    [ProducesResponseType(typeof(SubmitIdProofingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Submit(
        [FromBody] SubmitIdProofingRequest request,
        [FromServices] ICommandHandler<SubmitIdProofingCommand, SubmitIdProofingResponse> handler,
        [FromServices] IUserRepository userRepository,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserId(userRepository, cancellationToken);
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse("Unable to identify user from token."));
        }

        var command = new SubmitIdProofingCommand
        {
            UserId = userId.Value,
            DateOfBirth = request.DateOfBirth,
            IdType = request.IdType,
            IdValue = request.IdValue
        };

        var result = await handler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Gets the current verification status for a challenge.
    /// Polled by the frontend with exponential backoff after document capture.
    /// </summary>
    /// <param name="challengeId">The challenge's public GUID.</param>
    /// <param name="handler">The query handler.</param>
    /// <param name="userRepository">User repository for resolving user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Status retrieved.</response>
    /// <response code="401">User is not authenticated.</response>
    /// <response code="404">Challenge not found or belongs to a different user.</response>
    [HttpGet("status")]
    [ProducesResponseType(typeof(VerificationStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(
        [FromQuery] Guid challengeId,
        [FromServices] IQueryHandler<GetVerificationStatusQuery, VerificationStatusResponse> handler,
        [FromServices] IUserRepository userRepository,
        CancellationToken cancellationToken)
    {
        var userId = await ResolveUserId(userRepository, cancellationToken);
        if (userId == null)
        {
            return Unauthorized(new ErrorResponse("Unable to identify user from token."));
        }

        var query = new GetVerificationStatusQuery
        {
            ChallengeId = challengeId,
            UserId = userId.Value
        };

        var result = await handler.Handle(query, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Resolves the authenticated user's numeric ID from their email claim.
    /// </summary>
    private async Task<int?> ResolveUserId(IUserRepository userRepository, CancellationToken cancellationToken)
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.Identity?.Name;

        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("ID proofing request but email could not be extracted from claims");
            return null;
        }

        var user = await userRepository.GetUserByEmailAsync(email, cancellationToken);
        if (user == null)
        {
            logger.LogWarning("ID proofing request for email {Email} but user not found", email);
            return null;
        }

        return user.Id;
    }
}
