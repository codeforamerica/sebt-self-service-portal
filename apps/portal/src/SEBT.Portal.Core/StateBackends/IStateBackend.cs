using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends.Configuration;

namespace SEBT.Portal.Core.StateBackends;

public interface IStateBackend
{
    StateBackendCapabilities Capabilities { get; }

    Task<HouseholdLookupResult> LookupHouseholdAsync(
        HouseholdLookupRequest request, CancellationToken cancellationToken = default);

    // only if Capabilities.CardDetails.Modes has PerCase
    Task<CardDetails?> GetCardDetailsAsync(
        string caseId, CancellationToken cancellationToken = default);

    Task<CardReplacementResult> RequestCardReplacementAsync(
        CardReplacementRequest request, CancellationToken cancellationToken = default);

    // may report per-case failures when non-atomic
    Task<AddressUpdateResult> UpdateAddressAsync(
        AddressUpdateRequest request, CancellationToken cancellationToken = default);

    Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request, CancellationToken cancellationToken = default);

    Task<StateBackendHealth> GetHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record IdentitySignal(string Type, string Value, bool Verified);
public enum HouseholdLookupStatus { Found, NotFound, Ambiguous }
public sealed record HouseholdLookupRequest(IReadOnlyList<IdentitySignal> Signals);
public sealed record HouseholdLookupResult(HouseholdLookupStatus Status, HouseholdData? Household);

public sealed record CardDetails();

public sealed record AddressUpdateRequest();
public sealed record AddressUpdateResult();

public sealed record CardReplacementRequest();
public sealed record CardReplacementResult();

public sealed record EnrollmentCheckRequest();
public sealed record EnrollmentCheckResult();

public sealed record StateBackendHealth(bool IsHealthy);
