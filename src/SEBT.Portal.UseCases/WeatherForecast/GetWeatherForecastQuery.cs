using SEBT.Portal.Kernel;

namespace SEBT.Portal.UseCases.WeatherForecast;

public class GetWeatherForecastQuery : IQuery<GetWeatherForecastQueryResult>
{
    public static readonly GetWeatherForecastQuery Instance = new();
};
