namespace SEBT.Portal.Core.StateBackends.Configuration;

/// <summary>How card replacement is dispatched. This is derived from a complete <c>cardReplacement</c> operation</summary>
public enum CardReplacementCapability
{
    None,

    /// <summary>One backend call per decoded case token.</summary>
    PerCase,

    /// <summary>One backend call carrying every decoded case.</summary>
    Batch,
}

public sealed record StateBackendCapabilities(
    CardReplacementCapability CardReplacement,
    bool AddressUpdate,
    bool EnrollmentCheck);
