using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Core.StateBackends.Configuration;

public record StateBackendConfiguration
{
    public required Uri BaseUrl { get; init; }
    
    public required StateBackendAuthScheme Auth { get; init; }
    
    // public required StateBackendIdentifiersConfiguration Identifiers { get; init; }
    
    public required StateBackendOperations Operations { get; init; }

    public StateBackendCapabilities Capabilities =>
        new(
            "",
            ServiceMode: StateBackendServiceMode.Full,
            CardDetailsCapability.None, // TODO
            CardReplacementCapability.None, // TODO
            Operations.AddressUpdate != null,
            Operations.EnrollmentCheck != null);
}

// public record StateBackendIdentifiersConfiguration(string[] Preferred);
