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

    // Batch fan-out: one call carries every child as a correlated row; verdicts fan in by index.
    private async Task<EnrollmentCheckResult> CheckEnrollmentBatchAsync(
        EnrollmentCheckOperationConfig operation,
        EnrollmentRequestBinding binding,
        EnrollmentResponseMapping mapping,
        EnrollmentCheckRequest request,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage httpRequest = BuildRequest(operation);

        JsonArray rows = EnrollmentRequestBuilder.BuildRows(binding, request);
        httpRequest.Content = new StringContent(
            rows.ToJsonString(), Encoding.UTF8, "application/json");

        using JsonDocument document = await SendAndParseAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        return EnrollmentResponseCorrelator.Correlate(mapping, document.RootElement, request);
    }

    // PerChild fan-out: one call per child reading a single result object, aggregated in request order.
    private async Task<EnrollmentCheckResult> CheckEnrollmentPerChildAsync(
        EnrollmentCheckOperationConfig operation,
        EnrollmentRequestBinding binding,
        EnrollmentResponseMapping mapping,
        EnrollmentCheckRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<EnrollmentChildResult>(request.Children.Count);
        string? message = null;

        foreach (EnrollmentChild child in request.Children)
        {
            using HttpRequestMessage httpRequest = BuildRequest(operation);

            JsonObject body = EnrollmentRequestBuilder.BuildSingleChildBody(binding, child);
            httpRequest.Content = new StringContent(
                body.ToJsonString(), Encoding.UTF8, "application/json");

            using JsonDocument document = await SendAndParseAsync(httpRequest, cancellationToken)
                .ConfigureAwait(false);

            results.Add(EnrollmentResponseCorrelator.EvaluateSingleResult(
                mapping, document.RootElement, child.CheckId));

            // The result-level message is a single carrier over N per-child calls: the first
            // non-null one wins (only batch backends configure a messageField today).
            message ??= EnrollmentResponseCorrelator.ReadResultMessage(mapping, document.RootElement);
        }

        return new EnrollmentCheckResult(results, message);
    }

    public async Task<StateBackendHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        HealthOperationConfig? health = _configuration.Operations.Health
            ?? throw new NotSupportedException("Health check is not configured for the state backend.");

        // Unauthenticated liveness probe — no auth scheme applied.
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

        HouseholdData household = StateBackendResponseMapper.MapHousehold(
            document.RootElement, _configuration, mapping);

        if (household.SummerEbtCases.Count == 0)
        {
            return new HouseholdLookupResult(HouseholdLookupStatus.NotFound, Household: null);
        }

        return new HouseholdLookupResult(HouseholdLookupStatus.Found, household);
    }

    public async Task<WriteResult> RequestCardReplacementAsync(CardReplacementRequest request, CancellationToken cancellationToken = default)
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

        // Decode the opaque caseId into its routing fields, exposed (with the reason) to the binding.
        Dictionary<string, string> inputs = BuildCardReplacementInputs(request);

        return await ExecuteWriteAsync(
            operation,
            operation.Request,
            classifier,
            binding => StateBackendRequestBinder.BuildBody(binding, inputs),
            policyRejectionMessage: "The household is not eligible to request a replacement via the portal.",
            cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, string> BuildCardReplacementInputs(CardReplacementRequest request)
    {
        IReadOnlyDictionary<string, string> routingFields = OpaqueCaseId.Decode(request.CaseId);

        var inputs = new Dictionary<string, string>(routingFields, StringComparer.Ordinal);

        // "reason" is caller context, not a routing field.
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

    public async Task<WriteResult> UpdateAddressAsync(AddressUpdateRequest request, CancellationToken cancellationToken = default)
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

        return await ExecuteWriteAsync(
            operation,
            operation.Request,
            classifier,
            binding =>
            {
                // Decode the batch of opaque caseIds into their routing fields for the request binding.
                IReadOnlyList<IReadOnlyDictionary<string, string>> decodedCaseIds = request.CaseIds
                    .Select(OpaqueCaseId.Decode)
                    .ToList();

                Dictionary<string, string> addressInputs = BuildAddressInputs(request.Address);

                return StateBackendRequestBinder.BuildAddressBody(binding, decodedCaseIds, addressInputs);
            },
            policyRejectionMessage: "The household is not eligible to update their address via the portal.",
            cancellationToken).ConfigureAwait(false);
    }

    // Shared write pipeline: build request + optional bound body, attach the Idempotency-Key
    // (guards against a duplicate write on retry), send, classify the response into a WriteResult.
    private async Task<WriteResult> ExecuteWriteAsync(
        StateBackendOperationConfig operation,
        RequestBinding? binding,
        ResultClassifier classifier,
        Func<RequestBinding, JsonObject> buildBody,
        string policyRejectionMessage,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage httpRequest = BuildRequest(operation);

        if (binding is not null)
        {
            JsonObject body = buildBody(binding);
            httpRequest.Content = new StringContent(
                body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        httpRequest.Headers.Add("Idempotency-Key", _idempotencyKeyFactory());

        using HttpResponseMessage response = await _httpClient
            .SendAsync(httpRequest, cancellationToken)
            .ConfigureAwait(false);

        JsonElement? responseBody = await TryParseBodyAsync(response, cancellationToken).ConfigureAwait(false);

        WriteOutcome outcome = WriteResultClassifier.Classify(
            classifier, (int)response.StatusCode, responseBody);

        return ToWriteResult(outcome, policyRejectionMessage);
    }

    // Only non-null address scalars are included; a config mapping a field the address lacks fails
    // loud in the binder.
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

    private static WriteResult ToWriteResult(WriteOutcome outcome, string policyRejectionMessage) =>
        outcome switch
        {
            WriteOutcome.Success => WriteResult.Success(),
            WriteOutcome.PolicyRejection => WriteResult.PolicyRejected(
                "POLICY_REJECTION", policyRejectionMessage),
            WriteOutcome.BackendError => WriteResult.BackendError(
                "BACKEND_ERROR", "The state backend returned an error."),
            _ => throw new NotSupportedException(
                $"Write outcome '{outcome}' is not supported."),
        };
}
