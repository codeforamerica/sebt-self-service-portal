namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

public enum OperationTypes
{
    // Enrollment Checker
    EnrollmentCheck,

    // Portal
    HouseholdLookup,
    AddressUpdate,
    CardDetails, // Should we even consider the possibility of a separate endpoint here?
    CardReplacement,
    CardReplacementStatusCheck,

    // Control plane
    Health
}
