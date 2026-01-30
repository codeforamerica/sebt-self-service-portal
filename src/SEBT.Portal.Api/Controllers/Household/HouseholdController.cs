using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Models.Household;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Api.Controllers.Household;

/// <summary>
/// Controller for handling household data retrieval.
/// </summary>
[ApiController]
[Route("api/household")]
public class HouseholdController(ILogger<HouseholdController> logger) : ControllerBase
{

    /// <summary>
    /// Retrieves household data for the authenticated user.
    /// Address information is only included if ID verification has been completed.
    /// </summary>
    /// <param name="repository">The household repository for retrieving data.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An OK result with household data if found; otherwise, a NotFound result.</returns>
    /// <response code="200">Household data retrieved successfully.</response>
    /// <response code="401">User is not authorized (missing or invalid JWT token).</response>
    /// <response code="404">Household data not found for the authenticated user.</response>
    [HttpGet("data")]
    [Authorize]
    [ProducesResponseType(typeof(HouseholdDataResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHouseholdData(
        [FromServices] IHouseholdRepository repository,
        CancellationToken cancellationToken = default)
    {
        var email = GetUserEmail();

        if (string.IsNullOrWhiteSpace(email))
        {
            logger.LogWarning("Household data request attempted but email could not be extracted from claims");
            return Unauthorized(new ErrorResponse("Unable to identify user from token."));
        }

        // Normalize email to ensure consistency with repository
        var normalizedEmail = EmailNormalizer.Normalize(email);
        logger.LogDebug("Household data request received for email {Email}", normalizedEmail);

        // Check ID verification status from JWT claims
        var idProofingStatus = GetIdProofingStatus();
        var includeAddress = idProofingStatus == IdProofingStatus.Completed;

        if (includeAddress)
        {
            logger.LogDebug("Including address data for ID verified user {Email}", normalizedEmail);
        }

        var householdData = await repository.GetHouseholdByEmailAsync(
            normalizedEmail,
            includeAddress: includeAddress,
            cancellationToken);

        if (householdData == null)
        {
            logger.LogWarning("Household data not found for authenticated user");
            return NotFound(new ErrorResponse("Household data not found."));
        }

        logger.LogDebug("Household data retrieved successfully for email {Email}", normalizedEmail);
        return Ok(householdData.ToResponse());
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
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        if (!string.IsNullOrWhiteSpace(email))
        {
            return email;
        }

        var nameIdentifier = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.IsNullOrWhiteSpace(nameIdentifier))
        {
            logger.LogWarning("Using NameIdentifier claim as email fallback; NameIdentifier may not be an email address");
            return nameIdentifier;
        }

        var identityName = User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(identityName))
        {
            logger.LogWarning("Using User.Identity.Name as email fallback; this value may not be an email address");
            return identityName;
        }

        return null;
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
