using SEBT.Portal.Infrastructure.StateBackends;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class ConfigurableStateBackendCapabilitiesTests
{
    [Fact] // TODO - maybe theory w/ different yaml resources
    public void ConfigurableStateBackend_HasConfiguredCapabilities()
    {
        // Arrange
        var backend = new ConfigurableStateBackend();

        // Act
        var capabilities = backend.Capabilities;

        // Assert
        Assert.NotNull(capabilities);
    }
}
