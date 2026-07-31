using SEBT.Portal.Core.StateBackends;

namespace SEBT.Portal.Infrastructure.StateBackends;

/// <summary>
/// Throwing null-object bound to any port whose operation the config does not declare: reaching
/// one at runtime means a handler invoked an unconfigured capability, so it fails loud.
/// </summary>
public sealed class UnsupportedStateBackendOperation :
    ICardReplacementBackend,
    IAddressUpdateBackend,
    IEnrollmentCheckBackend,
    IHouseholdLookupBackend
{
    public Task<WriteResult> RequestCardReplacementAsync(
        CardReplacementRequest request, CancellationToken cancellationToken = default) =>
        throw NotDeclared("Card replacement");

    public Task<WriteResult> UpdateAddressAsync(
        AddressUpdateRequest request, CancellationToken cancellationToken = default) =>
        throw NotDeclared("Address update");

    public Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request, CancellationToken cancellationToken = default) =>
        throw NotDeclared("Enrollment check");

    public Task<HouseholdLookupResult> LookupHouseholdAsync(
        HouseholdLookupRequest request, CancellationToken cancellationToken = default) =>
        throw NotDeclared("Household lookup");

    private static NotSupportedException NotDeclared(string operation) =>
        new($"{operation} is not declared in this state's backend configuration.");
}
