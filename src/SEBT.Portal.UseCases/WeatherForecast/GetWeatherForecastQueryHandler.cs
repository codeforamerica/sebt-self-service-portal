using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.WeatherForecast;

public class GetWeatherForecastQueryHandler : IQueryHandler<GetWeatherForecastQuery, GetWeatherForecastQueryResult>
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };
    
    public Task<Result<GetWeatherForecastQueryResult>> Handle(
        GetWeatherForecastQuery query, 
        CancellationToken cancellationToken = default)
    {
        var result = new GetWeatherForecastQueryResult
        {
            WeatherForecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
                {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = Summaries[Random.Shared.Next(Summaries.Length)]
                })
                .ToArray()
        };

        return Task.FromResult(Result<GetWeatherForecastQueryResult>.Success(result));
    }
}