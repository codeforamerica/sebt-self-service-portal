using System.Net;
using RichardSzalay.MockHttp;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class ConfigurableStateBackendGetHealthTests
{
    private static StateBackendConfiguration BuildConfiguration() =>
        new()
        {
            BaseUrl = new Uri("http://backend.test"),
            Auth = new StateBackendApiKeyAuthScheme
            {
                Header = "X-Api-Key",
                KeyRef = "dc-api-key",
            },
            Operations = new StateBackendOperations
            {
                Health = new HealthOperationConfig
                {
                    Method = StateBackendHttpMethod.Get,
                    Path = "/health",
                },
            },
        };

    // Dispatches to the configured health endpoint and reports healthy iff the backend says OK.
    [Theory]
    [InlineData(HttpStatusCode.OK, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, false)]
    [InlineData(HttpStatusCode.InternalServerError, false)]
    public async Task GetHealthAsync_ReportsHealth_FromConfiguredEndpointStatus(
        HttpStatusCode status, bool expectedHealthy)
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Get, "http://backend.test/health")
            .Respond(status, "application/json", "{\"status\":\"ok\"}");

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(BuildConfiguration(), httpClient);

        // Act
        StateBackendHealth health = await backend.GetHealthAsync();

        // Assert
        Assert.Equal(expectedHealthy, health.IsHealthy);
    }

    // A connection-level failure (DNS, refused, timeout surfaced as HttpRequestException)
    // reports unhealthy rather than letting the exception escape the health probe.
    [Fact]
    public async Task GetHealthAsync_ConnectionFailure_ReportsUnhealthy()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Get, "http://backend.test/health")
            .Throw(new HttpRequestException("connection refused"));

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(BuildConfiguration(), httpClient);

        // Act
        StateBackendHealth health = await backend.GetHealthAsync();

        // Assert
        Assert.False(health.IsHealthy);
    }
}
