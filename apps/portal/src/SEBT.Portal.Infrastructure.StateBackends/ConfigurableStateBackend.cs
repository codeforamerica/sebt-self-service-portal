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

    public async Task<EnrollmentCheckResult> CheckEnrollmentAsync(EnrollmentCheckRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Capabilities.EnrollmentCheck)
        {
            throw new NotSupportedException("Enrollment check is not supported by the state backend.");
        }

        EnrollmentCheckOperationConfig operation = _configuration.Operations.EnrollmentCheck
            ?? throw new NotSupportedException("Enrollment check is not configured for the state backend.");

        EnrollmentRequestBinding binding = operation.Request
            ?? throw new NotSupportedException("Enrollment check has no request binding configured.");

        EnrollmentResponseMapping mapping = operation.Response
            ?? throw new NotSupportedException("Enrollment check has no response mapping configured.");

        // The callMode / indexField / expand / match combination is validated at config LOAD time
        // (StateBackendConfigurationValidator); the dispatch path trusts those invariants.
        return operation.CallMode switch
        {
            EnrollmentCallMode.Batch =>
                await CheckEnrollmentBatchAsync(operation, binding, mapping, request, cancellationToken)
                    .ConfigureAwait(false),
            EnrollmentCallMode.PerChild =>
                await CheckEnrollmentPerChildAsync(operation, binding, mapping, request, cancellationToken)
                    .ConfigureAwait(false),
            _ => throw new NotSupportedException(
                $"Unsupported enrollment callMode '{operation.CallMode}'."),
        };
    }

    // Batch fan-out (CO): ONE call carries every child as a correlated row; verdicts fan in by index.
    private async Task<EnrollmentCheckResult> CheckEnrollmentBatchAsync(
        EnrollmentCheckOperationConfig operation,
        EnrollmentRequestBinding binding,
        EnrollmentResponseMapping mapping,
        EnrollmentCheckRequest request,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage httpRequest = BuildRequest(operation);

        // Request-side candidate expansion: one row per child, plus a DOB-transposed candidate under
        // the same correlation index when the binding's expand strategy applies.
        JsonArray rows = EnrollmentRequestBuilder.BuildRows(binding, request);
        httpRequest.Content = new StringContent(
            rows.ToJsonString(), Encoding.UTF8, "application/json");

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

        // Response-side fan-in: a child matches when ANY of its candidate rows matched.
        return EnrollmentResponseCorrelator.Correlate(mapping, document.RootElement, request);
    }

    // PerChild fan-out (DC): the driver loops the batch and makes ONE call per child, reading a single
    // result object each, then aggregates the per-child verdicts in request order.
    private async Task<EnrollmentCheckResult> CheckEnrollmentPerChildAsync(
        EnrollmentCheckOperationConfig operation,
        EnrollmentRequestBinding binding,
        EnrollmentResponseMapping mapping,
        EnrollmentCheckRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<EnrollmentChildResult>(request.Children.Count);

        foreach (EnrollmentChild child in request.Children)
        {
            using HttpRequestMessage httpRequest = BuildRequest(operation);

            JsonObject body = EnrollmentRequestBuilder.BuildSingleChildBody(binding, child);
            httpRequest.Content = new StringContent(
                body.ToJsonString(), Encoding.UTF8, "application/json");

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

            bool isMatch = EnrollmentResponseCorrelator.EvaluateSingleResult(
                mapping, document.RootElement);
            results.Add(new EnrollmentChildResult(child.CheckId, isMatch));
        }

        return new EnrollmentCheckResult(results);
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

        // Classifier shape is validated at config LOAD time (StateBackendConfigurationValidator).

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

        WriteOutcome outcome = WriteResultClassifier.Classify(
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

    private static CardReplacementResult ToResult(WriteOutcome outcome) =>
        outcome switch
        {
            WriteOutcome.Success => CardReplacementResult.Success(),
            WriteOutcome.PolicyRejection => CardReplacementResult.PolicyRejected(
                "POLICY_REJECTION", "The household is not eligible to request a replacement via the portal."),
            WriteOutcome.BackendError => CardReplacementResult.BackendError(
                "BACKEND_ERROR", "The state backend returned an error."),
            _ => throw new NotSupportedException(
                $"Card-replacement outcome '{outcome}' is not supported."),
        };

    public async Task<AddressUpdateResult> UpdateAddressAsync(AddressUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!Capabilities.AddressUpdate)
        {
            throw new NotSupportedException("Address update is not supported by the state backend.");
        }

        AddressUpdateOperationConfig operation = _configuration.Operations.AddressUpdate
            ?? throw new NotSupportedException("Address update is not configured for the state backend.");

        ResultClassifier classifier = operation.Result
            ?? throw new NotSupportedException("Address update has no result classifier configured.");

        // Classifier shape is validated at config LOAD time (StateBackendConfigurationValidator).
        // Same capped 3-kind classifier as card replacement — no second classifier.

        using HttpRequestMessage httpRequest = BuildRequest(operation);

        if (operation.Request is { } binding)
        {
            // Decode the BATCH of opaque caseIds into their routing fields, then bind the body from
            // the shared/collect batch shapes plus the scalar address fields.
            IReadOnlyList<IReadOnlyDictionary<string, string>> decodedCaseIds = request.CaseIds
                .Select(OpaqueCaseId.Decode)
                .ToList();

            Dictionary<string, string> addressInputs = BuildAddressInputs(request.Address);

            JsonObject body = StateBackendRequestBinder.BuildAddressBody(
                binding, decodedCaseIds, addressInputs);
            httpRequest.Content = new StringContent(
                body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        // Idempotency-Key guards against a duplicate write on retry. Fresh per call in production;
        // injectable for deterministic tests.
        httpRequest.Headers.Add("Idempotency-Key", _idempotencyKeyFactory());

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        JsonElement? body2 = await TryParseBodyAsync(response, cancellationToken).ConfigureAwait(false);

        WriteOutcome outcome = WriteResultClassifier.Classify(
            classifier, (int)response.StatusCode, body2);

        return ToAddressResult(outcome);
    }

    // Address scalars exposed to the scalar request binding under fixed input names. Only non-null
    // fields are included; a config that maps a field the address lacks fails loud in the binder.
    private static Dictionary<string, string> BuildAddressInputs(AddressUpdateAddress address)
    {
        var inputs = new Dictionary<string, string>(StringComparer.Ordinal);

        if (address.Line1 is { } line1)
        {
            inputs["line1"] = line1;
        }

        if (address.Line2 is { } line2)
        {
            inputs["line2"] = line2;
        }

        if (address.City is { } city)
        {
            inputs["city"] = city;
        }

        if (address.State is { } state)
        {
            inputs["state"] = state;
        }

        if (address.Zip is { } zip)
        {
            inputs["zip"] = zip;
        }

        return inputs;
    }

    private static AddressUpdateResult ToAddressResult(WriteOutcome outcome) =>
        outcome switch
        {
            WriteOutcome.Success => AddressUpdateResult.Success(),
            WriteOutcome.PolicyRejection => AddressUpdateResult.PolicyRejected(
                "POLICY_REJECTION", "The household is not eligible to update their address via the portal."),
            WriteOutcome.BackendError => AddressUpdateResult.BackendError(
                "BACKEND_ERROR", "The state backend returned an error."),
            _ => throw new NotSupportedException(
                $"Address-update outcome '{outcome}' is not supported."),
        };
}
