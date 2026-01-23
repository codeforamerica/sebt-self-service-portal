using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Api.Models;
using SEBT.Portal.Kernel.Services;

namespace SEBT.Portal.Api.Controllers;

/// <summary>
/// Controller for handling feature flag queries.
/// </summary>
[ApiController]
[Route("api/features")]
public class FeaturesController : ControllerBase
{
    private readonly IFeatureFlagQueryService _featureFlagQueryService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeaturesController"/> class.
    /// </summary>
    /// <param name="featureFlagQueryService">The feature flag query service.</param>
    public FeaturesController(IFeatureFlagQueryService featureFlagQueryService)
    {
        _featureFlagQueryService = featureFlagQueryService;
    }

    /// <summary>
    /// Gets the current feature flag states.
    /// Returns flags from state-specific JSON files (appsettings.{State}.json), AWS AppConfig (if configured), or defaults.
    /// Flags are merged in priority order with state-specific JSON having the highest priority.
    /// State-specific configuration is loaded from appsettings.{State}.json files based on the STATE environment variable.
    /// Unknown flags are not included in the response.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>An OK result with feature flag states as JSON.</returns>
    /// <response code="200">Returns the current feature flag states.</response>
    /// <response code="499">The request was cancelled by the client.</response>
    /// <response code="500">An error occurred while retrieving feature flags.</response>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Dictionary<string, bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status499ClientClosedRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Tags("Features")]
    public async Task<IActionResult> GetFeatureFlags(CancellationToken cancellationToken = default)
    {
        try
        {
            var flags = await _featureFlagQueryService.GetFeatureFlagsAsync(cancellationToken);
            return Ok(flags);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(StatusCodes.Status499ClientClosedRequest, new ErrorResponse("Request was cancelled"));
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse("Failed to retrieve feature flags"));
        }
    }
}
