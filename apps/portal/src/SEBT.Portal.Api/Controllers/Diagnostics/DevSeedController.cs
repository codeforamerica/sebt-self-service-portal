using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Api.Controllers.Diagnostics;

/// <summary>
/// Development helpers for restoring individual seed personas between mutable E2E suites.
/// Available only when <see cref="SeedingSettings.EnableDevEndpoints"/> is true
/// (local and CI; not deployed lower environments). Excluded from OpenAPI.
/// </summary>
[ApiController]
[Route("api/dev/seed")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public class DevSeedController(
    IDatabaseSeeder databaseSeeder,
    IOptionsSnapshot<SeedingSettings> seedingSettings,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>
    /// Deletes and recreates a single known seed scenario user.
    /// </summary>
    [HttpPost("reseed/{scenarioName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ReseedScenario(
        [FromRoute] string scenarioName,
        CancellationToken cancellationToken)
    {
        if (!seedingSettings.Value.EnableDevEndpoints)
        {
            return NotFound();
        }

        var useMockHouseholdData = configuration.GetValue("UseMockHouseholdData", false);

        try
        {
            await databaseSeeder.ReseedUserScenarioAsync(
                scenarioName,
                useMockHouseholdData,
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        return NoContent();
    }
}
