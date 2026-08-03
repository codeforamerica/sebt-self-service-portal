namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

public record StateBackendOperations
{
    public EnrollmentCheckOperationConfig? EnrollmentCheck { get; init; }
    public HouseholdLookupOperationConfig? HouseholdLookup { get; init; }
    public AddressUpdateOperationConfig? AddressUpdate { get; init; }
    public CardReplacementOperationConfig? CardReplacement { get; init; }
    public HealthOperationConfig? Health { get; init; }
}
