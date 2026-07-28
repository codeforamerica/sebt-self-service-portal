using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;

namespace SEBT.Portal.Infrastructure.StateBackends;

public class ConfigurableStateBackend : IStateBackend
{
    private readonly StateBackendConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public ConfigurableStateBackend(StateBackendConfiguration configuration, HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(httpClient);

        _configuration = configuration;
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= _configuration.BaseUrl;
    }

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

    public async Task<StateBackendHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        HealthOperationConfig? health = _configuration.Operations.Health
            ?? throw new NotSupportedException("Health check is not configured for the state backend.");

        // /health is unauthenticated by design — no auth scheme applied here.
        using HttpRequestMessage request = BuildRequest(health);

        try
        {
            using HttpResponseMessage response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            return new StateBackendHealth(response.IsSuccessStatusCode);
        }
        catch (HttpRequestException)
        {
            return new StateBackendHealth(IsHealthy: false);
        }
    }

    private static HttpMethod ToHttpMethod(StateBackendHttpMethod method) =>
        method switch
        {
            StateBackendHttpMethod.Get => HttpMethod.Get,
            StateBackendHttpMethod.Patch => HttpMethod.Patch,
            StateBackendHttpMethod.Post => HttpMethod.Post,
            StateBackendHttpMethod.Put => HttpMethod.Put,
            _ => throw new NotSupportedException($"Unsupported HTTP method: {method}."),
        };

    private static HttpRequestMessage BuildRequest(StateBackendOperationConfig operation) =>
        new(ToHttpMethod(operation.Method), operation.Path);

    public async Task<HouseholdLookupResult> LookupHouseholdAsync(HouseholdLookupRequest request, CancellationToken cancellationToken = default)
    {
        HouseholdLookupOperationConfig? lookup = _configuration.Operations.HouseholdLookup
            ?? throw new NotSupportedException("Household lookup is not configured for the state backend.");

        StateBackendResponseMapping mapping = lookup.Response
            ?? throw new NotSupportedException("Household lookup has no response mapping configured.");

        // Auth (when configured) is applied by the HttpClient's handler chain, not here — keeps
        // the driver transport-agnostic and the auth scheme a reusable primitive.
        using HttpRequestMessage httpRequest = BuildRequest(lookup);

        if (lookup.Request is { } bindings)
        {
            JsonObject body = StateBackendRequestBinder.BuildBody(bindings, request.Signals);
            httpRequest.Content = new StringContent(
                body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using JsonDocument document = await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        HouseholdData household = StateBackendResponseMapper.MapHousehold(
            document.RootElement, _configuration, mapping);

        if (household.SummerEbtCases.Count == 0)
        {
            return new HouseholdLookupResult(HouseholdLookupStatus.NotFound, Household: null);
        }

        return new HouseholdLookupResult(HouseholdLookupStatus.Found, household);
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
