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

    [Fact]
    public async Task GetHealthAsync_DispatchesToConfiguredHealthEndpoint_AndReportsHealthy()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Get, "http://backend.test/health")
            .Respond(HttpStatusCode.OK, "application/json", "{\"status\":\"ok\"}");

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(BuildConfiguration(), httpClient);

        // Act
        StateBackendHealth health = await backend.GetHealthAsync();

        // Assert
        Assert.True(health.IsHealthy);
        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task GetHealthAsync_ReportsUnhealthy_WhenBackendReturnsError()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Get, "http://backend.test/health")
            .Respond(HttpStatusCode.ServiceUnavailable);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(BuildConfiguration(), httpClient);

        // Act
        StateBackendHealth health = await backend.GetHealthAsync();

        // Assert
        Assert.False(health.IsHealthy);
    }
}
