using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Api.Filters;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Models.IdProofing;
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
[ServiceFilter(typeof(ResolveUserFilter))]
public class IdProofingController : ControllerBase
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
        CancellationToken cancellationToken)
    {
        var userId = (Guid)HttpContext.Items[ResolveUserFilter.UserIdKey]!;

        var command = new SubmitIdProofingCommand
        {
            UserId = userId,
            DateOfBirth = $"{request.DateOfBirth.Year}-{request.DateOfBirth.Month.PadLeft(2, '0')}-{request.DateOfBirth.Day.PadLeft(2, '0')}",
            IdType = request.IdType,
            IdValue = request.IdValue,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            DiSessionToken = request.DiSessionToken
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
        CancellationToken cancellationToken)
    {
        var userId = (Guid)HttpContext.Items[ResolveUserFilter.UserIdKey]!;

        var query = new GetVerificationStatusQuery
        {
            ChallengeId = challengeId,
            UserId = userId
        };

        var result = await handler.Handle(query, cancellationToken);
        return result.ToActionResult();
    }
}
