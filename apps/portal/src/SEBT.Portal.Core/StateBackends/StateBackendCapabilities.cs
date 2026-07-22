namespace SEBT.Portal.Core.StateBackends;

public enum StateBackendServiceMode
{
    Full,
    ReadOnly,
    Maintenance
}

public enum CardDetailsCapability
{
    None,
    Batch,
    PerCase
}

public enum CardReplacementCapability
{
    None,
    Batch,
    PerCase
}

public sealed record StateBackendCapabilities(
    string SpecVersion,
    StateBackendServiceMode ServiceMode,
    CardDetailsCapability CardDetails,
    CardReplacementCapability CardReplacement,
    bool AddressUpdate,
    bool EnrollmentCheck);
