using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Kernel;

namespace SEBT.Portal.Api.Controllers;

/// <summary>
/// Controller for handling feature flag queries.
/// </summary>
[ApiController]
[Route("api/features")]
public class FeaturesController : ControllerBase
{
    private readonly IFeatureFlagService _featureFlagService;

    /// <summary>
    /// Initializes a new instance of the <see cref="FeaturesController"/> class.
    /// </summary>
    /// <param name="featureFlagService">The feature flag service.</param>
    public FeaturesController(IFeatureFlagService featureFlagService)
    {
        _featureFlagService = featureFlagService;
    }

    /// <summary>
    /// Gets the current feature flag states.
    /// Returns flags from the active state plugin (defaults) merged with configuration file settings (appsettings.{State}.json).
    /// Configuration file values take precedence over plugin defaults.
    /// Only flags that are explicitly configured (enabled or disabled) are returned.
    /// Unknown flags are not included in the response.
    /// </summary>
    /// <returns>An OK result with feature flag states as JSON.</returns>
    /// <response code="200">Returns the current feature flag states.</response>
    [HttpGet]
    [ProducesResponseType(typeof(Dictionary<string, bool>), StatusCodes.Status200OK)]
    public IActionResult GetFeatureFlags()
    {
        var flags = _featureFlagService.GetFeatureFlags();
        return Ok(flags);
    }
}
