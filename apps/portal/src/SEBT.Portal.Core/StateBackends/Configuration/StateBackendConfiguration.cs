using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;

namespace SEBT.Portal.Core.StateBackends.Configuration;

/// <summary>The C# records a state's YAML maps onto.</summary>
public record StateBackendConfiguration
{
    /// <summary>Default base URL. Set this, <see cref="BaseUrlRef"/>, or both.</summary>
    public Uri? BaseUrl { get; init; }

    /// <summary>
    /// Configuration key whose value, when present, overrides <see cref="BaseUrl"/> so the same
    /// bundle can target different environments.
    /// </summary>
    public string? BaseUrlRef { get; init; }

    public required StateBackendAuthScheme Auth { get; init; }

    public required StateBackendOperations Operations { get; init; }

    /// <summary>Named enum translation tables referenced by <see cref="Operations.FieldMapping.Enum"/>.</summary>
    public Dictionary<string, StateBackendEnumTable>? Enums { get; init; }

    /// <summary>
    /// Derived from which operations the config declares. This is a convenience property
    /// for the loader.
    /// </summary>
    public StateBackendCapabilities Capabilities =>
        new(
            Operations.CardReplacement switch
            {
                null => CardReplacementCapability.None,
                { CallMode: CardReplacementCallMode.Batch } => CardReplacementCapability.Batch,
                _ => CardReplacementCapability.PerCase,
            },
            Operations.AddressUpdate != null,
            Operations.EnrollmentCheck != null);
}
