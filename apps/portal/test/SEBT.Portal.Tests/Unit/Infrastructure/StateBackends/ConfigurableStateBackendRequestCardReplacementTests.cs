using System.Net;
using System.Text.Json;
using RichardSzalay.MockHttp;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class ConfigurableStateBackendRequestCardReplacementTests
{
    private const string FixedIdempotencyKey = "11111111-1111-1111-1111-111111111111";

    // Mirrors the DC REST wrapper's real contract: POST /card-replacements taking the sproc's
    // inputs (householdEmail, summerEbtCaseId) and returning its raw OUTPUT params — numeric
    // resultCode (0 = success) and resultMessage (policy rejections carry "policy" wording).
    private static CardReplacementOperationConfig DcCardReplacement() =>
        new()
        {
            Method = StateBackendHttpMethod.Post,
            Path = "/card-replacements",
            Request = new RequestBinding
            {
                Map = new Dictionary<string, string>
                {
                    ["caseId"] = "summerEbtCaseId",
                    ["householdEmail"] = "householdEmail",
                },
            },
            Result = new ResultClassifier
            {
                Conditions = new List<ResultCondition>
                {
                    new()
                    {
                        Outcome = WriteOutcome.PolicyRejection,
                        MessageField = "resultMessage",
                        MessageContains = new List<string> { "policy" },
                    },
                    new()
                    {
                        Outcome = WriteOutcome.Success,
                        Field = "resultCode",
                        ValueIn = new List<string> { "0" },
                    },
                },
                Default = WriteOutcome.BackendError,
            },
        };

    private static ResultClassifier CoResultClassifier() =>
        new()
        {
            Conditions = new List<ResultCondition>
            {
                new()
                {
                    Outcome = WriteOutcome.Success,
                    Field = "respCd",
                    ValueIn = new List<string> { "200", "00" },
                },
            },
            Default = WriteOutcome.BackendError,
        };

    private static ConfigurableStateBackend BuildBackend(
        MockHttpMessageHandler mockHttp, CardReplacementOperationConfig cardReplacement) =>
        new(
            StateBackendTestConfig.Base().WithCardReplacement(cardReplacement),
            mockHttp.ToHttpClient(),
            () => FixedIdempotencyKey);

    // An opaque caseId carrying the DC routing fields most tests decode on write. The token also
    // carries applicationId (composed on read for DC) even though card replacement doesn't bind it.
    private static string DefaultCaseId() =>
        OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = "SEBT-001",
            ["applicationId"] = "APP-100",
            ["householdEmail"] = "family@example.test",
        });

    // ---- 1. Opaque caseId round-trip ----------------------------------------------------------

    [Fact]
    public void OpaqueCaseId_ComposedOnRead_DecodesToSameRoutingFields_OnWrite()
    {
        // Arrange
        var routingFields = new Dictionary<string, string>
        {
            ["caseId"] = "SEBT-001",
            ["applicationId"] = "APP-100",
        };

        // Act
        string token = OpaqueCaseId.Compose(routingFields);
        IReadOnlyDictionary<string, string> decoded = OpaqueCaseId.Decode(token);

        // Assert — the token is opaque (doesn't expose raw values) and round-trips exactly.
        Assert.DoesNotContain("SEBT-001", token);
        Assert.Equal("SEBT-001", decoded["caseId"]);
        Assert.Equal("APP-100", decoded["applicationId"]);
    }

    [Fact]
    public void ResponseMapper_ComposesOpaqueCaseId_IntoSummerEbtCaseId()
    {
        // Arrange
        StateBackendConfiguration configuration = LookupWithCaseIdComposition();

        const string raw =
            """
            { "resultSets": [ [ { "CaseKey": "SEBT-001", "AppKey": "APP-100" } ] ] }
            """;

        // Act
        using JsonDocument document = JsonDocument.Parse(raw);
        var mapping = configuration.Operations.HouseholdLookup!.Response!;
        var household = StateBackendResponseMapper.MapHousehold(
            document.RootElement, configuration, mapping, new CaseIdContext());

        // Assert — the case's id is an opaque token decoding to the two routing fields.
        string token = Assert.Single(household.SummerEbtCases).SummerEBTCaseID!;
        IReadOnlyDictionary<string, string> decoded = OpaqueCaseId.Decode(token);
        Assert.Equal("SEBT-001", decoded["caseId"]);
        Assert.Equal("APP-100", decoded["applicationId"]);
    }

    [Fact]
    public void ResponseMapper_ComposesContextSourcedFields_AlongsideResponseFields()
    {
        // Arrange — the lookup response echoes no household email; fromContext sources it from the
        // lookup's caller context instead of a response column.
        StateBackendConfiguration configuration = LookupWithFromContextComposition();

        const string raw =
            """
            { "resultSets": [ [ { "CaseKey": "SEBT-001" } ] ] }
            """;

        // Act
        using JsonDocument document = JsonDocument.Parse(raw);
        var mapping = configuration.Operations.HouseholdLookup!.Response!;
        var household = StateBackendResponseMapper.MapHousehold(
            document.RootElement,
            configuration,
            mapping,
            new CaseIdContext { HouseholdIdentifier = "family@example.test" });

        // Assert — the token carries the response-sourced and context-sourced fields side by side.
        string token = Assert.Single(household.SummerEbtCases).SummerEBTCaseID!;
        IReadOnlyDictionary<string, string> decoded = OpaqueCaseId.Decode(token);
        Assert.Equal("SEBT-001", decoded["caseId"]);
        Assert.Equal("family@example.test", decoded["householdEmail"]);
    }

    [Fact]
    public void ResponseMapper_PacksEmptyContextField_WhenContextValueIsAbsent()
    {
        // Arrange — an existence-check lookup carries no household identifier; composition still
        // succeeds and packs empty, mirroring an absent response column. A later write that needs
        // the field fails loud instead.
        StateBackendConfiguration configuration = LookupWithFromContextComposition();

        const string raw =
            """
            { "resultSets": [ [ { "CaseKey": "SEBT-001" } ] ] }
            """;

        // Act
        using JsonDocument document = JsonDocument.Parse(raw);
        var mapping = configuration.Operations.HouseholdLookup!.Response!;
        var household = StateBackendResponseMapper.MapHousehold(
            document.RootElement, configuration, mapping, new CaseIdContext());

        // Assert
        string token = Assert.Single(household.SummerEbtCases).SummerEBTCaseID!;
        IReadOnlyDictionary<string, string> decoded = OpaqueCaseId.Decode(token);
        Assert.Equal(string.Empty, decoded["householdEmail"]);
    }

    // The DC write-path gap: DC's lookup response has NO household-email column, but both DC
    // writes bind householdEmail. fromContext packs it off the lookup's caller context so the
    // write can bind it from the decoded token.
    [Fact]
    public async Task RequestCardReplacementAsync_BindsHouseholdEmail_PackedFromLookupContext()
    {
        // Arrange — one config carrying both operations, DC-shaped.
        StateBackendConfiguration configuration =
            LookupWithFromContextComposition().WithCardReplacement(DcCardReplacement());

        string? capturedBody = null;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/households/lookup")
            .Respond("application/json", """{ "resultSets": [ [ { "CaseKey": "SEBT-001" } ] ] }""");
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/card-replacements")
            .With(message =>
            {
                capturedBody = message.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return true;
            })
            .Respond("application/json", """{ "resultCode": 0, "resultMessage": null }""");

        var backend = new ConfigurableStateBackend(
            configuration, mockHttp.ToHttpClient(), () => FixedIdempotencyKey);

        var lookupRequest = new HouseholdLookupRequest(
            new[] { new IdentitySignal("email", "family@example.test") })
        {
            HouseholdIdentifier = "family@example.test",
        };
        HouseholdLookupResult lookupResult = await backend.LookupHouseholdAsync(lookupRequest);
        string caseId = Assert.Single(lookupResult.Household!.SummerEbtCases).SummerEBTCaseID!;

        // Act — replay the composed token into the write, exactly as the portal would.
        WriteResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(new List<string> { caseId }));

        // Assert — the write body binds the context-packed email even though the lookup
        // response never carried one.
        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedBody);
        using JsonDocument document = JsonDocument.Parse(capturedBody);
        JsonElement root = document.RootElement;
        Assert.Equal("SEBT-001", root.GetProperty("summerEbtCaseId").GetString());
        Assert.Equal("family@example.test", root.GetProperty("householdEmail").GetString());
    }

    // DC-shaped composition whose householdEmail is context-sourced: the lookup response carries
    // only the case key, never the identifier the portal searched with.
    private static StateBackendConfiguration LookupWithFromContextComposition() =>
        StateBackendTestConfig.Base().WithLookup(new HouseholdLookupOperationConfig
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
                    },
                    FromContext = new Dictionary<string, string>
                    {
                        ["householdEmail"] = "householdIdentifier",
                    },
                },
            },
        });

    private static StateBackendConfiguration LookupWithCaseIdComposition() =>
        StateBackendTestConfig.Base().WithLookup(new HouseholdLookupOperationConfig
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
        });

    // ---- 2. Request body built from decoded caseId fields + constants/map + idempotency header --

    [Fact]
    public async Task RequestCardReplacementAsync_BuildsBody_FromDecodedCaseIdFields()
    {
        // Arrange
        string caseId = DefaultCaseId();

        string? capturedBody = null;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/card-replacements")
            .With(message =>
            {
                capturedBody = message.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return true;
            })
            .Respond("application/json", """{ "resultCode": 0, "resultMessage": null }""");

        var backend = BuildBackend(mockHttp, DcCardReplacement());
        var request = new CardReplacementRequest(new List<string> { caseId });

        // Act
        await backend.RequestCardReplacementAsync(request);

        // Assert — only the mapped routing fields travel; unmapped token fields (applicationId)
        // and the wrapper's optional `reason` stay out of the body.
        Assert.NotNull(capturedBody);
        using JsonDocument document = JsonDocument.Parse(capturedBody);
        JsonElement root = document.RootElement;

        Assert.Equal("SEBT-001", root.GetProperty("summerEbtCaseId").GetString());
        Assert.Equal("family@example.test", root.GetProperty("householdEmail").GetString());
        Assert.Equal(2, root.EnumerateObject().Count());
    }

    [Fact]
    public async Task RequestCardReplacementAsync_AttachesIdempotencyKeyHeader()
    {
        // Arrange
        string caseId = DefaultCaseId();

        string? capturedKey = null;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/card-replacements")
            .With(message =>
            {
                if (message.Headers.TryGetValues("Idempotency-Key", out IEnumerable<string>? values))
                {
                    capturedKey = values.First();
                }

                return true;
            })
            .Respond("application/json", """{ "resultCode": 0, "resultMessage": null }""");

        var backend = BuildBackend(mockHttp, DcCardReplacement());

        // Act
        await backend.RequestCardReplacementAsync(new CardReplacementRequest(new List<string> { caseId }));

        // Assert — the injected key is attached (a per-call UUID in production).
        Assert.Equal(FixedIdempotencyKey, capturedKey);
    }

    // ---- 3. Result classification (3-kind, first-match-wins) -----------------------------------

    // The wrapper always answers HTTP 200; the sproc's OUTPUT params carry the outcome. Fixtures
    // mirror the mock sproc's real shapes (numeric resultCode, resultMessage wording).
    [Theory]
    // resultCode 0 → Success (the real success shape: message is null).
    [InlineData("""{ "resultCode": 0, "resultMessage": null }""", HttpStatusCode.OK, true, false)]
    // The real policy rejection: resultCode 1 with policy wording in the message.
    [InlineData(
        """{ "resultCode": 1, "resultMessage": "Policy Failure: household is not eligible for a replacement." }""",
        HttpStatusCode.OK, false, true)]
    // resultCode is the success value but the policy-message condition is evaluated FIRST
    // (order is load-bearing), so the policy message beats the later success condition.
    [InlineData(
        """{ "resultCode": 0, "resultMessage": "Rejected by household eligibility policy" }""",
        HttpStatusCode.OK, false, true)]
    // Non-policy failure: nothing matches → default BackendError (still HTTP 200).
    [InlineData(
        """{ "resultCode": 1, "resultMessage": "Backend Failure: the request could not be completed." }""",
        HttpStatusCode.OK, false, false)]
    public async Task RequestCardReplacementAsync_ClassifiesOutcome_FirstMatchWins(
        string responseJson, HttpStatusCode status, bool isSuccess, bool isPolicyRejection)
    {
        // Arrange
        string caseId = DefaultCaseId();

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/card-replacements")
            .Respond(status, "application/json", responseJson);

        var backend = BuildBackend(mockHttp, DcCardReplacement());

        // Act
        WriteResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(new List<string> { caseId }));

        // Assert
        Assert.Equal(isSuccess, result.IsSuccess);
        Assert.Equal(isPolicyRejection, result.IsPolicyRejection);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_ClassifiesSuccess_FromCoRespCd_StatusAndValueKinds()
    {
        // Arrange — the value-in-set kind: respCd in {200, 00}.
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
        WriteResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(new List<string> { caseId }));

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
                    new() { Outcome = WriteOutcome.Success, StatusIn = new List<int> { 200, 201 } },
                },
                Default = WriteOutcome.BackendError,
            },
        };

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/cards/replace")
            .Respond(HttpStatusCode.Created, "application/json", "{}");

        var backend = BuildBackend(mockHttp, cardReplacement);

        // Act
        WriteResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(new List<string> { caseId }));

        // Assert
        Assert.True(result.IsSuccess);
    }

    // ---- Backend message propagation: generic text only when the backend supplied none ---------

    // The backend's own message flows through — never the generic prose — whether the condition
    // matched (policy wording) or nothing did (default BackendError reading the declared messageField).
    [Theory]
    [InlineData(
        "Policy Failure: household is not eligible for a replacement.",
        "POLICY_REJECTION", true)]
    [InlineData(
        "Backend Failure: the request could not be completed.",
        "BACKEND_ERROR", false)]
    public async Task RequestCardReplacementAsync_PropagatesBackendMessage_OnFailure(
        string backendMessage, string expectedCode, bool expectedPolicyRejection)
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/card-replacements")
            .Respond(
                "application/json",
                $$"""{ "resultCode": 1, "resultMessage": "{{backendMessage}}" }""");

        var backend = BuildBackend(mockHttp, DcCardReplacement());

        // Act
        WriteResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(new List<string> { DefaultCaseId() }));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(expectedPolicyRejection, result.IsPolicyRejection);
        Assert.Equal(expectedCode, result.ErrorCode);
        Assert.Equal(backendMessage, result.ErrorMessage);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_FallsBackToGenericMessage_WhenBackendSuppliesNone()
    {
        // Arrange — CO's classifier declares no messageField, so there is no backend message to read.
        string caseId = OpaqueCaseId.Compose(new Dictionary<string, string> { ["caseId"] = "CO-001" });

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
            .Respond("application/json", """{ "respCd": "500" }""");

        var backend = BuildBackend(mockHttp, cardReplacement);

        // Act
        WriteResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(new List<string> { caseId }));

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("The state backend returned an error.", result.ErrorMessage);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_FallsBackToGenericPolicyMessage_WhenBackendSuppliesNone()
    {
        // Arrange — a status-driven policy rejection with an empty body: no message to propagate.
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
                    new() { Outcome = WriteOutcome.PolicyRejection, StatusIn = new List<int> { 422 } },
                    new() { Outcome = WriteOutcome.Success, StatusIn = new List<int> { 200 } },
                },
                Default = WriteOutcome.BackendError,
            },
        };

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/cards/replace")
            .Respond(HttpStatusCode.UnprocessableEntity, "application/json", "{}");

        var backend = BuildBackend(mockHttp, cardReplacement);

        // Act
        WriteResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(new List<string> { caseId }));

        // Assert
        Assert.True(result.IsPolicyRejection);
        Assert.Equal(
            "The household is not eligible to request a replacement via the portal.",
            result.ErrorMessage);
    }

    // ---- 4. Batch fan-out: one call per caseId, fail fast on the first non-success -------------

    [Fact]
    public async Task RequestCardReplacementAsync_SendsOneCallPerCaseId()
    {
        // Arrange — two caseIds routing to two different raw cases.
        string caseId1 = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = "SEBT-001",
            ["householdEmail"] = "family@example.test",
        });
        string caseId2 = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = "SEBT-002",
            ["householdEmail"] = "family@example.test",
        });

        var capturedCaseIds = new List<string?>();
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/card-replacements")
            .With(message =>
            {
                string body = message.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                using JsonDocument document = JsonDocument.Parse(body);
                capturedCaseIds.Add(document.RootElement.GetProperty("summerEbtCaseId").GetString());
                return true;
            })
            .Respond("application/json", """{ "resultCode": 0, "resultMessage": null }""");

        var backend = BuildBackend(mockHttp, DcCardReplacement());

        // Act
        WriteResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(new List<string> { caseId1, caseId2 }));

        // Assert — one POST per decoded caseId, in request order.
        Assert.True(result.IsSuccess);
        Assert.Equal(new List<string?> { "SEBT-001", "SEBT-002" }, capturedCaseIds);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_FailsFast_OnFirstNonSuccess()
    {
        // Arrange — the first case classifies BackendError; the second must never be sent.
        string caseId1 = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = "SEBT-001",
            ["householdEmail"] = "family@example.test",
        });
        string caseId2 = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["caseId"] = "SEBT-002",
            ["householdEmail"] = "family@example.test",
        });

        int callCount = 0;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/card-replacements")
            .With(_ =>
            {
                callCount++;
                return true;
            })
            .Respond(
                HttpStatusCode.OK,
                "application/json",
                """{ "resultCode": 1, "resultMessage": "Backend Failure: the request could not be completed." }""");

        var backend = BuildBackend(mockHttp, DcCardReplacement());

        // Act
        WriteResult result = await backend.RequestCardReplacementAsync(
            new CardReplacementRequest(new List<string> { caseId1, caseId2 }));

        // Assert — the failing result is returned and the loop stops.
        Assert.False(result.IsSuccess);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task RequestCardReplacementAsync_FailsLoud_OnEmptyCaseIds()
    {
        var backend = BuildBackend(new MockHttpMessageHandler(), DcCardReplacement());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            backend.RequestCardReplacementAsync(new CardReplacementRequest(new List<string>())));
    }

    // ---- Fail-loud classifier config validation ------------------------------------------------

    public static TheoryData<string, ResultCondition, string> MalformedConditions() =>
        new()
        {
            {
                // Setting BOTH statusIn and valueIn is not one of the 3 closed kinds.
                "two kinds",
                new ResultCondition
                {
                    Outcome = WriteOutcome.Success,
                    StatusIn = new List<int> { 200 },
                    ValueIn = new List<string> { "OK" },
                    Field = "resultCode",
                },
                "exactly one"
            },
            {
                // No kind set at all.
                "no kind",
                new ResultCondition { Outcome = WriteOutcome.Success },
                "exactly one"
            },
            {
                // The value-in-set kind requires a source field.
                "valueIn without field",
                new ResultCondition { Outcome = WriteOutcome.Success, ValueIn = new List<string> { "OK" } },
                "field"
            },
        };

    [Theory]
    [MemberData(nameof(MalformedConditions))]
    public void Validate_FailsLoud_OnMalformedCondition(
        string label, ResultCondition condition, string expectedMessageFragment)
    {
        // Arrange
        _ = label; // row identifier only
        var classifier = new ResultClassifier
        {
            Conditions = new List<ResultCondition> { condition },
        };

        // Act + Assert
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => WriteResultClassifier.Validate(classifier));
        Assert.Contains(expectedMessageFragment, ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
