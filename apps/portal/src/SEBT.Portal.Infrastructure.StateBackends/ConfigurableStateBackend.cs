using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;

namespace SEBT.Portal.Infrastructure.StateBackends;

public class ConfigurableStateBackend :
    IHouseholdLookupBackend,
    IStateBackendHealth
{
    private readonly StateBackendConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly Func<string> _idempotencyKeyFactory;

    public ConfigurableStateBackend(StateBackendConfiguration configuration, HttpClient httpClient)
        : this(configuration, httpClient, () => Guid.NewGuid().ToString())
    {
    }

    // Idempotency-key factory is injectable for deterministic tests; production yields a fresh UUID.
    public ConfigurableStateBackend(
        StateBackendConfiguration configuration, HttpClient httpClient, Func<string> idempotencyKeyFactory)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(idempotencyKeyFactory);

        _configuration = configuration;
        _httpClient = httpClient;
        _idempotencyKeyFactory = idempotencyKeyFactory;
        _httpClient.BaseAddress ??= _configuration.BaseUrl;
    }

    // Capability guards derive from the loaded configuration; capabilities are not part of the ports.
    private StateBackendCapabilities Capabilities =>
        _configuration.Capabilities;

    public async Task<StateBackendHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        HealthOperationConfig? health = _configuration.Operations.Health
            ?? throw new NotSupportedException("Health check is not configured for the state backend.");

        // Liveness probe; the shared handler chain still applies the state's auth scheme.
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

    // Shared read-path plumbing: send, fail loud on a non-success status, parse the JSON body.
    private async Task<JsonDocument> SendAndParseAsync(
        HttpRequestMessage httpRequest, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        await using Stream stream = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        return await JsonDocument
            .ParseAsync(stream, cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<HouseholdLookupResult> LookupHouseholdAsync(HouseholdLookupRequest request, CancellationToken cancellationToken = default)
    {
        HouseholdLookupOperationConfig? lookup = _configuration.Operations.HouseholdLookup
            ?? throw new NotSupportedException("Household lookup is not configured for the state backend.");

        StateBackendResponseMapping mapping = lookup.Response
            ?? throw new NotSupportedException("Household lookup has no response mapping configured.");

        // Auth (when configured) is applied by the HttpClient's handler chain, not here.
        using HttpRequestMessage httpRequest = BuildRequest(lookup);

        if (lookup.Request is { } binding)
        {
            JsonObject body = StateBackendRequestBinder.BuildBody(binding, request);
            httpRequest.Content = new StringContent(
                body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using JsonDocument document = await SendAndParseAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        // Caller context for caseId composition: identifiers a later write must route by that the
        // lookup response never echoes.
        var caseIdContext = new CaseIdContext { HouseholdIdentifier = request.HouseholdIdentifier };

        HouseholdData household = StateBackendResponseMapper.MapHousehold(
            document.RootElement, _configuration, mapping, caseIdContext);

        // A lookup mapping zero cases reads as NotFound — mirrors the plugin path's contract.
        if (household.SummerEbtCases.Count == 0)
        {
            return new HouseholdLookupResult(HouseholdLookupStatus.NotFound, Household: null);
        }

        return new HouseholdLookupResult(HouseholdLookupStatus.Found, household);
    }
}
