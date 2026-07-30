using SEBT.Portal.Core.StateBackends;

namespace SEBT.Portal.Infrastructure.StateBackends;

/// <summary>
/// Throwing null-object bound to any port whose operation the loaded state-backend config does
/// not declare. The capability decision is made once, at composition, from config presence —
/// reaching one of these at runtime means a handler invoked a capability the state never
/// configured, so it fails loud. <see cref="ConfigurableStateBackend"/> keeps equivalent
/// internal guards as defense-in-depth.
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
