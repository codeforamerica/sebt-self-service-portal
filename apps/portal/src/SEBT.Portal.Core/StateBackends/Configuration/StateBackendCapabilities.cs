namespace SEBT.Portal.Core.StateBackends.Configuration;

public enum CardReplacementCapability
{
    None,
    Batch,
    PerCase
}

public sealed record StateBackendCapabilities(
    CardReplacementCapability CardReplacement,
    bool AddressUpdate,
    bool EnrollmentCheck);
