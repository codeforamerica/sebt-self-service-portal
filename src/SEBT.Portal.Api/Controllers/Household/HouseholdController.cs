using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Models.Household;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.Household;

namespace SEBT.Portal.Api.Controllers.Household;

/// <summary>
/// Controller for household data retrieval and management.
/// Household lookup uses state-configurable preferred household ID type (e.g. email, SNAP ID) resolved from the authenticated user.
/// </summary>
[ApiController]
[Route("api/household")]
public class HouseholdController : ControllerBase
{
    /// <summary>
    /// Retrieves household data for the authenticated user.
    /// The household identifier used for lookup is determined by state configuration (e.g. email, SNAP ID).
    /// PII data is only included when the user meets the ID proofing requirements configured for the state.
    /// </summary>
    /// <param name="queryHandler">The use case handler for retrieving household data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An OK result with household data if found; otherwise, NotFound or Unauthorized.</returns>
    /// <response code="200">Household data retrieved successfully.</response>
    /// <response code="401">User is not authorized or no household identifier could be resolved from token.</response>
    /// <response code="404">Household data not found for the authenticated user.</response>
    [HttpGet("data")]
    [Authorize]
    [ProducesResponseType(typeof(HouseholdDataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHouseholdData(
        [FromServices] IQueryHandler<GetHouseholdDataQuery, Core.Models.Household.HouseholdData> queryHandler,
        CancellationToken cancellationToken = default)
    {
        var query = new GetHouseholdDataQuery { User = User };
        var result = await queryHandler.Handle(query, cancellationToken);

        return result.ToActionResult(
            successMap: data => Ok(data.ToResponse()),
            failureMap: r => r switch
            {
                UnauthorizedResult<Core.Models.Household.HouseholdData> unauthorized => Unauthorized(new ErrorResponse(unauthorized.Message)),
                PreconditionFailedResult<Core.Models.Household.HouseholdData> preconditionFailed => NotFound(new ErrorResponse(preconditionFailed.Message)),
                _ => StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse("An unexpected error occurred."))
            });
    }

    /// <summary>
    /// Updates the mailing address for the authenticated user's household.
    /// </summary>
    /// <param name="request">The new mailing address.</param>
    /// <param name="commandHandler">The use case handler for updating the address.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>No content on success; otherwise, BadRequest or Unauthorized.</returns>
    /// <response code="204">Address updated successfully.</response>
    /// <response code="400">Validation failed (missing fields, invalid format, or address could not be verified).</response>
    /// <response code="403">User is not authorized or no household identifier could be resolved from token.</response>
    /// <response code="502">Address verification provider error or timeout.</response>
    [HttpPut("address")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> UpdateAddress(
        [FromBody] UpdateAddressRequest request,
        [FromServices] ICommandHandler<UpdateAddressCommand> commandHandler,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateAddressCommand
        {
            User = User,
            StreetAddress1 = request.StreetAddress1,
            StreetAddress2 = request.StreetAddress2,
            City = request.City,
            State = request.State,
            PostalCode = request.PostalCode
        };

        var result = await commandHandler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }

    /// <summary>
    /// Requests replacement cards for the authenticated user's household.
    /// </summary>
    /// <param name="request">The case IDs to request replacements for.</param>
    /// <param name="commandHandler">The use case handler for requesting card replacements.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>No content on success; otherwise, BadRequest, Forbidden, or NotFound.</returns>
    /// <response code="204">Card replacement request recorded successfully.</response>
    /// <response code="400">Validation failed (no cases selected or cooldown active).</response>
    /// <response code="403">User is not authorized or no household identifier could be resolved from token.</response>
    /// <response code="404">Household data not found for the authenticated user.</response>
    [HttpPost("cards/replace")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RequestCardReplacement(
        [FromBody] RequestCardReplacementRequest request,
        [FromServices] ICommandHandler<RequestCardReplacementCommand> commandHandler,
        CancellationToken cancellationToken = default)
    {
        var command = new RequestCardReplacementCommand
        {
            User = User,
            CaseIds = request.CaseIds
        };

        var result = await commandHandler.Handle(command, cancellationToken);
        return result.ToActionResult();
    }
}
