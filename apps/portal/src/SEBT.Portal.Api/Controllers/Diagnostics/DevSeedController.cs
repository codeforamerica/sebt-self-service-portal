using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Api.Controllers.Diagnostics;

/// <summary>
/// Development helpers for restoring individual seed personas between mutable E2E suites.
/// Available only when ASPNETCORE_ENVIRONMENT is Development.
/// </summary>
[ApiController]
[Route("api/dev/seed")]
[AllowAnonymous]
[Tags("Diagnostics")]
public class DevSeedController(
    IDatabaseSeeder databaseSeeder,
    IWebHostEnvironment environment,
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
        if (!environment.IsDevelopment())
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
