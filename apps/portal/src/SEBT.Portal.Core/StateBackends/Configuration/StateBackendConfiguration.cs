using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Core.StateBackends.Configuration;

public record StateBackendConfiguration
{
    public required Uri BaseUrl { get; init; }

    public required StateBackendAuthScheme Auth { get; init; }

    public required StateBackendOperations Operations { get; init; }

    /// <summary>Named enum translation tables referenced by <see cref="Operations.FieldMapping.Enum"/>.</summary>
    public Dictionary<string, StateBackendEnumTable>? Enums { get; init; }

    public StateBackendCapabilities Capabilities =>
        new(
            Operations.CardReplacement != null
                ? CardReplacementCapability.PerCase
                : CardReplacementCapability.None,
            Operations.AddressUpdate != null,
            Operations.EnrollmentCheck != null);
}
