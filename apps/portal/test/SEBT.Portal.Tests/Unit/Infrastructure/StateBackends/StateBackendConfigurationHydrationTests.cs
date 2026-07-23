using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends.Configuration;
using SEBT.Portal.Tests.Unit.Infrastructure.StateBackends.ConfigSamples;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

/// <summary>
/// Spike (DC-568): can the canonical state-backend config records hydrate from YAML via
/// YamlDotNet 18.1.0 WITHOUT modifying the Core types? Deserializer config is inline on
/// purpose — this is an experiment, not a committed Infrastructure loader.
/// </summary>
public class StateBackendConfigurationHydrationTests
{
    [Fact]
    public void Hydrates_StateBackendConfiguration_FromEmbeddedYaml()
    {
        string yaml = SampleLoader.Load("dc.sample.yaml");
        var config = StateBackendConfigurationLoader.Load(yaml);

        Assert.Equal(new Uri("http://localhost:8085"), config.BaseUrl);

        StateBackendApiKeyAuthScheme apiKeyAuth = Assert.IsType<StateBackendApiKeyAuthScheme>(config.Auth);
        Assert.Equal("X-Api-Key", apiKeyAuth.Header);
        Assert.Equal("dc-api-key", apiKeyAuth.KeyRef);

        HouseholdLookupOperationConfig? householdLookup = config.Operations.HouseholdLookup;
        Assert.NotNull(householdLookup);
        Assert.Equal(StateBackendHttpMethod.Post, householdLookup.Method);
        Assert.Equal("/households/lookup", householdLookup.Path);

        Assert.NotNull(config.Operations.Health);

        // Capability-derivation smoke assert: nothing modeled AddressUpdate/EnrollmentCheck here.
        StateBackendCapabilities capabilities = config.Capabilities;
        Assert.False(capabilities.AddressUpdate);
        Assert.False(capabilities.EnrollmentCheck);
    }
}
