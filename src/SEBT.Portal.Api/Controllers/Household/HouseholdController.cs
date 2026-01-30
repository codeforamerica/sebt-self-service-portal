using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Models.Household;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Api.Controllers.Household;

/// <summary>
/// Controller for handling household data retrieval.
/// Household lookup uses state-configurable preferred household ID type (e.g. email, SNAP ID) resolved from the authenticated user.
/// </summary>
[ApiController]
[Route("api/household")]
public class HouseholdController(ILogger<HouseholdController> logger) : ControllerBase
{
    /// <summary>
    /// Retrieves household data for the authenticated user.
    /// The household identifier used for lookup is determined by state configuration (e.g. email, SNAP ID).
    /// Address information is only included if ID verification has been completed.
    /// </summary>
    /// <param name="resolver">Resolves the household identifier from the user's claims based on state config.</param>
    /// <param name="repository">The household repository for retrieving data.</param>
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
        [FromServices] IHouseholdIdentifierResolver resolver,
        [FromServices] IHouseholdRepository repository,
        CancellationToken cancellationToken = default)
    {
        var identifier = await resolver.ResolveAsync(User, cancellationToken);

        if (identifier == null)
        {
            logger.LogWarning("Household data request attempted but no household identifier could be resolved from claims");
            return Unauthorized(new ErrorResponse("Unable to identify user from token."));
        }

        logger.LogDebug("Household data request received for identifier {Type}={Value}", identifier.Type, identifier.Value);

        var idProofingStatus = GetIdProofingStatus();
        var includeAddress = idProofingStatus == IdProofingStatus.Completed;

        if (includeAddress)
        {
            logger.LogDebug("Including address data for ID verified user");
        }

        var householdData = await repository.GetHouseholdByIdentifierAsync(
            identifier,
            includeAddress: includeAddress,
            cancellationToken);

        if (householdData == null)
        {
            logger.LogWarning("Household data not found for authenticated user");
            return NotFound(new ErrorResponse("Household data not found."));
        }

        logger.LogDebug("Household data retrieved successfully for identifier {Type}={Value}", identifier.Type, identifier.Value);
        return Ok(householdData.ToResponse());
    }

    /// <summary>
    /// Extracts the ID proofing status from the authenticated user's claims.
    /// </summary>
    /// <returns>The ID proofing status, or NotStarted if not found.</returns>
    private IdProofingStatus GetIdProofingStatus()
    {
        var statusClaim = User.FindFirst(JwtClaimTypes.IdProofingStatus)?.Value;

        if (string.IsNullOrWhiteSpace(statusClaim))
        {
            logger.LogWarning("ID proofing status claim not found in token, defaulting to NotStarted");
            return IdProofingStatus.NotStarted;
        }

        if (int.TryParse(statusClaim, out var statusValue) &&
            Enum.IsDefined(typeof(IdProofingStatus), statusValue))
        {
            return (IdProofingStatus)statusValue;
        }

        logger.LogWarning("Invalid ID proofing status claim value: {StatusClaim}, defaulting to NotStarted", statusClaim);
        return IdProofingStatus.NotStarted;
    }
}
