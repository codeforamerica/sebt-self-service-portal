using Microsoft.AspNetCore.Mvc;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.AspNetCore;
using SEBT.Portal.UseCases.WeatherForecast;

namespace SEBT.Portal.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeatherForecastController(ILogger<WeatherForecastController> logger) : ControllerBase
{
    private readonly ILogger<WeatherForecastController> _logger = logger;

    [HttpGet(Name = "GetWeatherForecast")]
    public async Task<IActionResult> Get(
        [FromServices] IQueryHandler<GetWeatherForecastQuery, GetWeatherForecastQueryResult> handler)
    {
        var result = await handler.Handle(GetWeatherForecastQuery.Instance);

        return result.ToActionResult(r => new OkObjectResult(r.WeatherForecasts));
    }
}
