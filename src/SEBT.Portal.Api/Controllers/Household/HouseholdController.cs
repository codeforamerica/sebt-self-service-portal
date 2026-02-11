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
/// Controller for handling household data retrieval.
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
}
