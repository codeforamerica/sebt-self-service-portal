using Microsoft.Extensions.DependencyInjection;
using SEBT.Portal.Kernel;
using SEBT.Portal.UseCases.WeatherForecast;

namespace SEBT.Portal.UseCases;

public static class Dependencies
{
    public static IServiceCollection AddUseCases(this IServiceCollection services)
    {
        services.RegisterQueryHandler<GetWeatherForecastQuery, GetWeatherForecastQueryResult, GetWeatherForecastQueryHandler>();
        
        return services;
    }
}
