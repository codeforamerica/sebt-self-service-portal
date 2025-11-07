namespace SEBT.Portal.UseCases.WeatherForecast;

public class GetWeatherForecastQueryResult
{
    public required IEnumerable<WeatherForecast> WeatherForecasts { get; init; }
}
