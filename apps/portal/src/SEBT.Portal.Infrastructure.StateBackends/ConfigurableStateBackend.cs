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
    private readonly Func<string> _idempotencyKeyFactory;

    public ConfigurableStateBackend(StateBackendConfiguration configuration, HttpClient httpClient)
        : this(configuration, httpClient, () => Guid.NewGuid().ToString())
    {
    }

    // The idempotency-key factory is injectable so tests can assert a deterministic key. In
    // production it generates a fresh UUID per call.
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

        if (lookup.Request is { } binding)
        {
            JsonObject body = StateBackendRequestBinder.BuildBody(binding, request);
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

    public async Task<CardReplacementResult> RequestCardReplacementAsync(CardReplacementRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Capabilities.CardReplacement == CardReplacementCapability.None)
        {
            throw new NotSupportedException("Card replacement is not supported by the state backend.");
        }

        CardReplacementOperationConfig operation = _configuration.Operations.CardReplacement
            ?? throw new NotSupportedException("Card replacement is not configured for the state backend.");

        ResultClassifier classifier = operation.Result
            ?? throw new NotSupportedException("Card replacement has no result classifier configured.");

        // Fail loud on a malformed classifier before performing the call.
        CardReplacementClassifier.Validate(classifier);

        // Decode the opaque caseId into its routing fields, then expose them (plus the request's
        // reason) as inputs to the domain-centered request binding.
        Dictionary<string, string> inputs = BuildCardReplacementInputs(request);

        using HttpRequestMessage httpRequest = BuildRequest(operation);

        if (operation.Request is { } binding)
        {
            JsonObject body = StateBackendRequestBinder.BuildBody(binding, inputs);
            httpRequest.Content = new StringContent(
                body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        // Idempotency-Key guards against duplicate replacement issuance on retry. Fresh per call
        // in production; injectable for deterministic tests.
        httpRequest.Headers.Add("Idempotency-Key", _idempotencyKeyFactory());

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        JsonElement? body2 = await TryParseBodyAsync(response, cancellationToken).ConfigureAwait(false);

        CardReplacementOutcome outcome = CardReplacementClassifier.Classify(
            classifier, (int)response.StatusCode, body2);

        return ToResult(outcome);
    }

    private static Dictionary<string, string> BuildCardReplacementInputs(CardReplacementRequest request)
    {
        IReadOnlyDictionary<string, string> routingFields = OpaqueCaseId.Decode(request.CaseId);

        var inputs = new Dictionary<string, string>(routingFields, StringComparer.Ordinal);

        // "reason" is caller context, not a routing field — a fixed pass-through input name.
        if (request.Reason is { } reason)
        {
            inputs["reason"] = reason;
        }

        return inputs;
    }

    private static async Task<JsonElement?> TryParseBodyAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            await using Stream stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            if (stream.CanSeek && stream.Length == 0)
            {
                return null;
            }

            using JsonDocument document = await JsonDocument
                .ParseAsync(stream, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            // Clone so the element survives the JsonDocument being disposed.
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            // A non-JSON body still classifies on status-only conditions / the default.
            return null;
        }
    }

    private static CardReplacementResult ToResult(CardReplacementOutcome outcome) =>
        outcome switch
        {
            CardReplacementOutcome.Success => CardReplacementResult.Success(),
            CardReplacementOutcome.PolicyRejection => CardReplacementResult.PolicyRejected(
                "POLICY_REJECTION", "The household is not eligible to request a replacement via the portal."),
            CardReplacementOutcome.BackendError => CardReplacementResult.BackendError(
                "BACKEND_ERROR", "The state backend returned an error."),
            _ => throw new NotSupportedException(
                $"Card-replacement outcome '{outcome}' is not supported."),
        };

    public Task<AddressUpdateResult> UpdateAddressAsync(AddressUpdateRequest request, CancellationToken cancellationToken = default)
    {
        if (!Capabilities.AddressUpdate)
        {
            throw new NotSupportedException("Address update is not supported by the state backend.");
        }

        throw new NotImplementedException();
    }
}
