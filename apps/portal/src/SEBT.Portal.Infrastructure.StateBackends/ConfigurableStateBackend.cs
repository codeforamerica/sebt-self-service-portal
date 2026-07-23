using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;

namespace SEBT.Portal.Infrastructure.StateBackends;

public class ConfigurableStateBackend(StateBackendConfiguration configuration) 
    : IStateBackend
{
    private readonly StateBackendConfiguration _configuration = configuration;

    public StateBackendCapabilities Capabilities => 
        _configuration.Capabilities;

    public Task<EnrollmentCheckResult> CheckEnrollmentAsync(EnrollmentCheckRequest request, CancellationToken cancellationToken = default)
    {
        if (!Capabilities.EnrollmentCheck)
        {
            throw new NotSupportedException("Enrollment check is not supported by the state backend.");
        }

        throw new NotImplementedException();
    }

    public Task<CardDetails?> GetCardDetailsAsync(string caseId, CancellationToken cancellationToken = default)
    {
        if (Capabilities.CardDetails == CardDetailsCapability.None)
        {
            throw new NotSupportedException("Fetching card details is not supported by the state backend.");
        }

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
        if (Capabilities.CardReplacement == CardReplacementCapability.None)
        {
            throw new NotSupportedException("Card replacement is not supported by the state backend.");
        }

        throw new NotImplementedException();
    }

    public Task<AddressUpdateResult> UpdateAddressAsync(AddressUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (!Capabilities.AddressUpdate)
        {
            throw new NotSupportedException("Address update is not supported by the state backend.");
        }

        throw new NotImplementedException();
    }
}
