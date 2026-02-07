using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Api.Models.Household;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;

namespace SEBT.Portal.Api.Controllers.Household;

/// <summary>
/// Controller for handling household data retrieval.
/// </summary>
[ApiController]
[Route("api/household")]
public class HouseholdController(
    ILogger<HouseholdController> logger,
    IIdProofingRequirementsService idProofingRequirementsService) : ControllerBase
{
    /// <summary>
    /// Retrieves household data for the authenticated user.
    /// PII data is only included when the user meets the
    /// ID proofing requirements configured for the state.
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

        // Determine PII visibility based on user's IAL level and state configuration
        var userIalLevel = GetUserIalLevel();
        var piiVisibility = idProofingRequirementsService.GetPiiVisibility(userIalLevel);

        logger.LogDebug(
            "PII visibility for user {Email} (IalLevel={IalLevel}): Address={IncludeAddress}, Email={IncludeEmail}, Phone={IncludePhone}",
            normalizedEmail,
            userIalLevel,
            piiVisibility.IncludeAddress,
            piiVisibility.IncludeEmail,
            piiVisibility.IncludePhone);

        var householdData = await repository.GetHouseholdByEmailAsync(
            normalizedEmail,
            piiVisibility,
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
    /// Extracts the user's IAL level from the authenticated user's claims.
    /// Uses "ial" claim only (values: "0", "1", "1plus", "2"). Id proofing status is a separate concern.
    /// </summary>
    /// <returns>The user's IAL level, or None if not found or invalid.</returns>
    private UserIalLevel GetUserIalLevel()
    {
        var ialClaim = User.FindFirst(JwtClaimTypes.Ial)?.Value;
        if (!string.IsNullOrWhiteSpace(ialClaim))
        {
            var normalized = ialClaim.Trim().ToLowerInvariant();
            if (normalized == "1") return UserIalLevel.IAL1;
            if (normalized == "1plus") return UserIalLevel.IAL1plus;
            if (normalized == "2") return UserIalLevel.IAL2;
            if (normalized == "0") return UserIalLevel.None;
            logger.LogWarning("Invalid IAL claim value: {IalClaim}, defaulting to None", ialClaim);
        }
        else
        {
            logger.LogWarning("IAL claim not found in token, defaulting to None");
        }

        return UserIalLevel.None;
    }
}
