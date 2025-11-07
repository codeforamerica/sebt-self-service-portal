using SEBT.Portal.Kernel.Results;
using SEBT.Portal.UseCases.WeatherForecast;

namespace SEBT.Portal.UseCases.Tests;

public class GetWeatherForecastQueryHandlerTests
{
    [Fact]
    public async Task Handle()
    {
        // Arrange
        var handler = new GetWeatherForecastQueryHandler();

        // Act
        var result = await handler.Handle(GetWeatherForecastQuery.Instance);

        // Assert
        var successResult = Assert.IsType<SuccessResult<GetWeatherForecastQueryResult>>(result);
        Assert.Equal(5, successResult.Value.WeatherForecasts.Count());
    }
}
