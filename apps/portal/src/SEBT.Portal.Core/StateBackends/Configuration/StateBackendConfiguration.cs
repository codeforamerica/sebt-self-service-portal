using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Core.StateBackends.Configuration;

public record StateBackendConfiguration
{
    public required Uri BaseUrl { get; init; }

    public required StateBackendAuthScheme Auth { get; init; }

    // public required StateBackendIdentifiersConfiguration Identifiers { get; init; }

    public required StateBackendOperations Operations { get; init; }

    /// <summary>
    /// Named, domain-centered enum translation tables referenced by response field mappings
    /// (see <see cref="Operations.FieldMapping.Enum"/>). Null when the backend maps no enums.
    /// </summary>
    public Dictionary<string, StateBackendEnumTable>? Enums { get; init; }

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
