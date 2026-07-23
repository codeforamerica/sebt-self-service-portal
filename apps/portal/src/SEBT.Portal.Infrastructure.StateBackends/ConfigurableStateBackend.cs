using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;

namespace SEBT.Portal.Infrastructure.StateBackends;

public class ConfigurableStateBackend : IStateBackend
{
    public StateBackendCapabilities Capabilities => throw new NotImplementedException();

    public Task<EnrollmentCheckResult> CheckEnrollmentAsync(EnrollmentCheckRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<CardDetails?> GetCardDetailsAsync(string caseId, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<StateBackendHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<HouseholdLookupResult> LookupHouseholdAsync(HouseholdLookupRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<CardReplacementResult> RequestCardReplacementAsync(CardReplacementRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<AddressUpdateResult> UpdateAddressAsync(AddressUpdateRequest request, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
