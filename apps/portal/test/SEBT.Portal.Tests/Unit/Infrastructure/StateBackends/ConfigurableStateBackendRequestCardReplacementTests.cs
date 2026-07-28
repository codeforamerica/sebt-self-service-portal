using System.Net;
using System.Text.Json;
using RichardSzalay.MockHttp;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class ConfigurableStateBackendRequestCardReplacementTests
{
    private const string FixedIdempotencyKey = "11111111-1111-1111-1111-111111111111";

    // Domain-centered DC card-replacement binding. The map inputs are the routing fields the
    // opaque caseId decodes to (summerEbtCaseId, applicationId) plus a caller-context value.
    private static CardReplacementOperationConfig DcCardReplacement() =>
        new()
        {
            Method = StateBackendHttpMethod.Post,
            Path = "/cards/replace",
            Request = new RequestBinding
            {
                Constants = new Dictionary<string, object>
                {
                    ["source"] = "portal",
                },
                // Map LHS are decoded caseId routing fields (caseId, applicationId) plus the
                // caller-context "reason"; RHS are the backend's write-body target paths.
                Map = new Dictionary<string, string>
                {
                    ["caseId"] = "summerEbtCaseId",
                    ["applicationId"] = "applicationId",
                    ["reason"] = "reason",
                },
            },
            Result = new ResultClassifier
            {
                // DC: policy rejection when the message mentions "policy"; success on result code.
                Conditions = new List<ResultCondition>
                {
                    new()
                    {
                        Outcome = CardReplacementOutcome.PolicyRejection,
                        MessageField = "message",
                        MessageContains = new List<string> { "policy" },
                    },
                    new()
                    {
                        Outcome = CardReplacementOutcome.Success,
                        Field = "resultCode",
                        ValueIn = new List<string> { "OK" },
                    },
                },
                Default = CardReplacementOutcome.BackendError,
            },
        };

    // CO card-replacement classifier: success on respCd in {200, 00}.
    private static ResultClassifier CoResultClassifier() =>
        new()
        {
            Conditions = new List<ResultCondition>
            {
                new()
                {
                    Outcome = CardReplacementOutcome.Success,
                    Field = "respCd",
                    ValueIn = new List<string> { "200", "00" },
                },
            },
            Default = CardReplacementOutcome.BackendError,
        };

    private static StateBackendConfiguration BuildConfiguration(CardReplacementOperationConfig cardReplacement) =>
        new()
        {
            BaseUrl = new Uri("http://backend.test"),
            Auth = new StateBackendApiKeyAuthScheme { Header = "X-Api-Key", KeyRef = "dc-api-key" },
            Operations = new StateBackendOperations
            {
                CardReplacement = cardReplacement,
            },
        };

    private static ConfigurableStateBackend BuildBackend(
        MockHttpMessageHandler mockHttp, CardReplacementOperationConfig cardReplacement) =>
        new(BuildConfiguration(cardReplacement), mockHttp.ToHttpClient(), () => FixedIdempotencyKey);

    // ---- 1. Opaque caseId round-trip ----------------------------------------------------------

    [Fact]
    public void OpaqueCaseId_ComposedOnRead_DecodesToSameRoutingFields_OnWrite()
    {
        // Arrange — the routing fields a write needs, keyed by OUR names.
        var routingFields = new Dictionary<string, string>
        {
            ["caseId"] = "SEBT-001",
            ["applicationId"] = "APP-100",
        };

        // Act — compose on the read side, decode on the write side.
        string token = OpaqueCaseId.Compose(routingFields);
        IReadOnlyDictionary<string, string> decoded = OpaqueCaseId.Decode(token);

        // Assert — token is opaque (not the raw values) and round-trips exactly.
        Assert.DoesNotContain("SEBT-001", token);
        Assert.Equal("SEBT-001", decoded["caseId"]);
        Assert.Equal("APP-100", decoded["applicationId"]);
    }

    [Fact]
    public void ResponseMapper_ComposesOpaqueCaseId_IntoSummerEbtCaseId()
    {
        // Arrange — a read whose caseId is composed from two backend fields.
        StateBackendConfiguration configuration = LookupWithCaseIdComposition();

        const string raw =
            """
            { "resultSets": [ [ { "CaseKey": "SEBT-001", "AppKey": "APP-100" } ] ] }
            """;

        // Act
        using JsonDocument document = JsonDocument.Parse(raw);
        var mapping = configuration.Operations.HouseholdLookup!.Response!;
        var household = StateBackendResponseMapper.MapHousehold(document.RootElement, configuration, mapping);

        // Assert — the case's id is an opaque token that decodes to the two routing fields.
        string token = Assert.Single(household.SummerEbtCases).SummerEBTCaseID!;
        IReadOnlyDictionary<string, string> decoded = OpaqueCaseId.Decode(token);
        Assert.Equal("SEBT-001", decoded["caseId"]);
        Assert.Equal("APP-100", decoded["applicationId"]);
    }

    private static StateBackendConfiguration LookupWithCaseIdComposition() =>
        new()
        {
            BaseUrl = new Uri("http://backend.test"),
            Auth = new StateBackendApiKeyAuthScheme { Header = "X-Api-Key", KeyRef = "k" },
            Operations = new StateBackendOperations
            {
                HouseholdLookup = new HouseholdLookupOperationConfig
                {
                    Method = StateBackendHttpMethod.Post,
                    Path = "/households/lookup",
                    Response = new StateBackendResponseMapping
                    {
                        Root = "$.resultSets[0]",
                        Fields = new Dictionary<string, FieldMapping>
                        {
                            ["childFirstName"] = new() { From = "CaseKey" }, // placeholder to keep a field present
                        },
                        CaseId = new CaseIdComposition
                        {
                            Fields = new Dictionary<string, string>
                            {
                                ["caseId"] = "CaseKey",
                                ["applicationId"] = "AppKey",
                            },
                        },
                    },
                },
            },
        };

    // ---- 2. Request body built from decoded caseId fields + constants/map + idempotency header --

    [Fact]
    public async Task RequestCardReplacementAsync_BuildsBody_FromDecodedCaseIdFieldsAndConstants()
    {
        // Arrange — an opaque caseId carrying the routing fields.
        string caseId = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = "SEBT-001",
            ["applicationId"] = "APP-100",
        });

        string? capturedBody = null;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/cards/replace")
            .With(message =>
            {
                capturedBody = message.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return true;
            })
            .Respond("application/json", """{ "resultCode": "OK" }""");

        var backend = BuildBackend(mockHttp, DcCardReplacement());
        var request = new CardReplacementRequest(caseId) { Reason = "lost" };

        // Act
        await backend.RequestCardReplacementAsync(request);

        // Assert
        Assert.NotNull(capturedBody);
        using JsonDocument document = JsonDocument.Parse(capturedBody);
        JsonElement root = document.RootElement;

        Assert.Equal("portal", root.GetProperty("source").GetString());
        Assert.Equal("SEBT-001", root.GetProperty("summerEbtCaseId").GetString());
        Assert.Equal("APP-100", root.GetProperty("applicationId").GetString());
        Assert.Equal("lost", root.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task RequestCardReplacementAsync_AttachesIdempotencyKeyHeader()
    {
        // Arrange
        string caseId = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = "SEBT-001",
            ["applicationId"] = "APP-100",
        });

        string? capturedKey = null;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/cards/replace")
            .With(message =>
            {
                if (message.Headers.TryGetValues("Idempotency-Key", out IEnumerable<string>? values))
                {
                    capturedKey = values.First();
                }

                return true;
            })
            .Respond("application/json", """{ "resultCode": "OK" }""");

        var backend = BuildBackend(mockHttp, DcCardReplacement());

        // Act
        await backend.RequestCardReplacementAsync(new CardReplacementRequest(caseId) { Reason = "lost" });

        // Assert — the injected key is attached (per-call UUID in production).
        Assert.Equal(FixedIdempotencyKey, capturedKey);
    }

    // ---- 3. Result classification (3-kind, first-match-wins) -----------------------------------

    [Fact]
    public async Task RequestCardReplacementAsync_ClassifiesSuccess_FromResultCodeValueInSet()
    {
        // Arrange
        string caseId = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = "SEBT-001",
            ["applicationId"] = "APP-100",
        });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/cards/replace")
            .Respond("application/json", """{ "resultCode": "OK", "message": "issued" }""");

        var backend = BuildBackend(mockHttp, DcCardReplacement());

        // Act
        CardReplacementResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(caseId) { Reason = "lost" });

        // Assert
        Assert.True(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_ClassifiesPolicyRejection_FromDcPolicyMessage()
    {
        // Arrange
        string caseId = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = "SEBT-001",
            ["applicationId"] = "APP-100",
        });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/cards/replace")
            // resultCode is OK, but the policy message is evaluated FIRST (order is load-bearing).
            .Respond(
                HttpStatusCode.OK,
                "application/json",
                """{ "resultCode": "OK", "message": "Rejected by household eligibility policy" }""");

        var backend = BuildBackend(mockHttp, DcCardReplacement());

        // Act
        CardReplacementResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(caseId) { Reason = "lost" });

        // Assert — first-match-wins: policy message beats the later success condition.
        Assert.False(result.IsSuccess);
        Assert.True(result.IsPolicyRejection);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_ClassifiesBackendError_ByDefault_WhenNoConditionMatches()
    {
        // Arrange
        string caseId = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = "SEBT-001",
            ["applicationId"] = "APP-100",
        });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/cards/replace")
            .Respond(
                HttpStatusCode.InternalServerError,
                "application/json",
                """{ "resultCode": "ERR", "message": "boom" }""");

        var backend = BuildBackend(mockHttp, DcCardReplacement());

        // Act
        CardReplacementResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(caseId) { Reason = "lost" });

        // Assert — nothing matches → default BackendError.
        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_ClassifiesSuccess_FromCoRespCd_StatusAndValueKinds()
    {
        // Arrange — CO grounding: respCd in {200, 00}. Exercises the value-in-set kind.
        string caseId = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = "CO-001",
        });

        var cardReplacement = new CardReplacementOperationConfig
        {
            Method = StateBackendHttpMethod.Post,
            Path = "/cards/replace",
            Request = new RequestBinding
            {
                Map = new Dictionary<string, string> { ["caseId"] = "sebtChldCwin" },
            },
            Result = CoResultClassifier(),
        };

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/cards/replace")
            .Respond("application/json", """{ "respCd": "00" }""");

        var backend = BuildBackend(mockHttp, cardReplacement);

        // Act
        CardReplacementResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(caseId));

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_ClassifiesSuccess_FromHttpStatusInSet()
    {
        // Arrange — the status-in kind: 200/201 => success.
        string caseId = OpaqueCaseId.Compose(new Dictionary<string, string> { ["caseId"] = "SEBT-001" });

        var cardReplacement = new CardReplacementOperationConfig
        {
            Method = StateBackendHttpMethod.Post,
            Path = "/cards/replace",
            Request = new RequestBinding { Map = new Dictionary<string, string> { ["caseId"] = "caseId" } },
            Result = new ResultClassifier
            {
                Conditions = new List<ResultCondition>
                {
                    new() { Outcome = CardReplacementOutcome.Success, StatusIn = new List<int> { 200, 201 } },
                },
                Default = CardReplacementOutcome.BackendError,
            },
        };

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/cards/replace")
            .Respond(HttpStatusCode.Created, "application/json", "{}");

        var backend = BuildBackend(mockHttp, cardReplacement);

        // Act
        CardReplacementResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(caseId));

        // Assert
        Assert.True(result.IsSuccess);
    }

    // ---- Fail-loud classifier config validation ------------------------------------------------

    [Fact]
    public void Validate_FailsLoud_WhenConditionSetsMoreThanOneKind()
    {
        // Arrange — a condition that sets BOTH statusIn and valueIn is not one of the 3 closed kinds.
        var classifier = new ResultClassifier
        {
            Conditions = new List<ResultCondition>
            {
                new()
                {
                    Outcome = CardReplacementOutcome.Success,
                    StatusIn = new List<int> { 200 },
                    ValueIn = new List<string> { "OK" },
                    Field = "resultCode",
                },
            },
        };

        // Act + Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => CardReplacementClassifier.Validate(classifier));
        Assert.Contains("exactly one", ex.Message);
    }

    [Fact]
    public void Validate_FailsLoud_WhenConditionSetsNoKind()
    {
        // Arrange — a condition with no kind set at all.
        var classifier = new ResultClassifier
        {
            Conditions = new List<ResultCondition>
            {
                new() { Outcome = CardReplacementOutcome.Success },
            },
        };

        // Act + Assert
        Assert.Throws<InvalidOperationException>(() => CardReplacementClassifier.Validate(classifier));
    }

    [Fact]
    public void Validate_FailsLoud_WhenValueInHasNoField()
    {
        // Arrange — value-in-set kind requires a source field.
        var classifier = new ResultClassifier
        {
            Conditions = new List<ResultCondition>
            {
                new() { Outcome = CardReplacementOutcome.Success, ValueIn = new List<string> { "OK" } },
            },
        };

        // Act + Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => CardReplacementClassifier.Validate(classifier));
        Assert.Contains("field", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
