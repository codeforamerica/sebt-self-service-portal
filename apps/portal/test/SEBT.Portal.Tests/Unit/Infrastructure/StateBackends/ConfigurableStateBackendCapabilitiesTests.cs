using RichardSzalay.MockHttp;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class ConfigurableStateBackendCapabilitiesTests
{
    [Fact] // TODO - maybe theory w/ different yaml resources
    public void ConfigurableStateBackend_HasConfiguredCapabilities()
    {
        // Arrange
        var configuration = new StateBackendConfiguration
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
        var httpClient = new MockHttpMessageHandler().ToHttpClient();
        var backend = new ConfigurableStateBackend(configuration, httpClient);

        // Act
        var capabilities = backend.Capabilities;

        // Assert
        Assert.NotNull(capabilities);
    }
}
