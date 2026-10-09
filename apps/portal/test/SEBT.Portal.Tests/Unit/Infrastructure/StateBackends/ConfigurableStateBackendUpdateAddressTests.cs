using System.Net;
using System.Text.Json;
using RichardSzalay.MockHttp;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class ConfigurableStateBackendUpdateAddressTests
{
    private const string FixedIdempotencyKey = "22222222-2222-2222-2222-222222222222";

    // DC: the household email is SHARED across every decoded caseId; address scalars bind via the map.
    private static AddressUpdateOperationConfig DcAddressUpdate() =>
        new()
        {
            Method = StateBackendHttpMethod.Post,
            Path = "/households/address",
            Request = new RequestBinding
            {
                Constants = new Dictionary<string, object>
                {
                    ["source"] = "portal",
                },
                // One household identifier resolved across all caseIds; fails loud on disagreement.
                Shared = new Dictionary<string, string>
                {
                    ["householdEmail"] = "householdIdentifier",
                },
                Map = new Dictionary<string, string>
                {
                    ["line1"] = "address.line1",
                    ["city"] = "address.city",
                    ["state"] = "address.state",
                    ["zip"] = "address.zip",
                },
            },
            Result = new ResultClassifier
            {
                Conditions = new List<ResultCondition>
                {
                    new()
                    {
                        Outcome = WriteOutcome.Success,
                        Field = "resultCode",
                        ValueIn = new List<string> { "OK" },
                    },
                },
                Default = WriteOutcome.BackendError,
            },
        };

    // CO: each decoded caseId's per-case write-id is COLLECTED into an array.
    private static AddressUpdateOperationConfig CoAddressUpdate() =>
        new()
        {
            Method = StateBackendHttpMethod.Patch,
            Path = "/sebt/update-std-dtls",
            Request = new RequestBinding
            {
                Collect = new Dictionary<string, string>
                {
                    ["writeId"] = "cases",
                },
                Map = new Dictionary<string, string>
                {
                    ["line1"] = "stdAddr",
                    ["zip"] = "stdZip",
                },
            },
            Result = new ResultClassifier
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
            },
        };

    private static ConfigurableStateBackend BuildBackend(
        MockHttpMessageHandler mockHttp, AddressUpdateOperationConfig addressUpdate) =>
        new(
            StateBackendTestConfig.Base().WithAddressUpdate(addressUpdate),
            mockHttp.ToHttpClient(),
            () => FixedIdempotencyKey);

    private static AddressUpdateAddress SampleAddress() =>
        new()
        {
            Line1 = "123 Main St",
            City = "Washington",
            State = "DC",
            Zip = "20001",
        };

    // ---- 1. caseId batch decode ------------------------------------------------------------------

    [Fact]
    public void OpaqueCaseIds_Batch_EachDecodesToItsOwnRoutingFields()
    {
        // Arrange — two cases sharing a household email but each with its own write-id.
        string a = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["writeId"] = "W-1",
            ["householdEmail"] = "family@example.test",
        });
        string b = OpaqueCaseId.Compose(new Dictionary<string, string>
        {
            ["writeId"] = "W-2",
            ["householdEmail"] = "family@example.test",
        });

        // Act
        IReadOnlyDictionary<string, string> da = OpaqueCaseId.Decode(a);
        IReadOnlyDictionary<string, string> db = OpaqueCaseId.Decode(b);

        // Assert
        Assert.Equal("W-1", da["writeId"]);
        Assert.Equal("W-2", db["writeId"]);
        Assert.Equal("family@example.test", da["householdEmail"]);
        Assert.Equal("family@example.test", db["householdEmail"]);
    }

    // ---- 2. DC body: shared householdEmail + address scalars, classified -------------------------

    [Fact]
    public async Task UpdateAddressAsync_Dc_BuildsBody_FromSharedHouseholdEmailAndAddressScalars()
    {
        // Arrange — two caseIds agreeing on the shared household email.
        var caseIds = new List<string>
        {
            OpaqueCaseId.Compose(new Dictionary<string, string>
            {
                ["writeId"] = "W-1",
                ["householdEmail"] = "family@example.test",
            }),
            OpaqueCaseId.Compose(new Dictionary<string, string>
            {
                ["writeId"] = "W-2",
                ["householdEmail"] = "family@example.test",
            }),
        };

        string? capturedBody = null;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/households/address")
            .With(message =>
            {
                capturedBody = message.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return true;
            })
            .Respond("application/json", """{ "resultCode": "OK" }""");

        var backend = BuildBackend(mockHttp, DcAddressUpdate());
        var request = new AddressUpdateRequest("family@example.test", caseIds, SampleAddress());

        // Act
        WriteResult result = await backend.UpdateAddressAsync(request);

        // Assert
        Assert.NotNull(capturedBody);
        using JsonDocument document = JsonDocument.Parse(capturedBody);
        JsonElement root = document.RootElement;

        Assert.Equal("portal", root.GetProperty("source").GetString());
        Assert.Equal("family@example.test", root.GetProperty("householdIdentifier").GetString());
        JsonElement address = root.GetProperty("address");
        Assert.Equal("123 Main St", address.GetProperty("line1").GetString());
        Assert.Equal("Washington", address.GetProperty("city").GetString());
        Assert.Equal("DC", address.GetProperty("state").GetString());
        Assert.Equal("20001", address.GetProperty("zip").GetString());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateAddressAsync_Dc_ClassifiesBackendError_ByDefault()
    {
        // Arrange
        var caseIds = new List<string>
        {
            OpaqueCaseId.Compose(new Dictionary<string, string>
            {
                ["writeId"] = "W-1",
                ["householdEmail"] = "family@example.test",
            }),
        };

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/households/address")
            .Respond(
                HttpStatusCode.InternalServerError,
                "application/json",
                """{ "resultCode": "ERR" }""");

        var backend = BuildBackend(mockHttp, DcAddressUpdate());

        // Act
        WriteResult result = await backend.UpdateAddressAsync(
            new AddressUpdateRequest("family@example.test", caseIds, SampleAddress()));

        // Assert — nothing matches → default BackendError. DC's classifier declares no
        // messageField, so the generic fallback text applies.
        Assert.False(result.IsSuccess);
        Assert.False(result.IsPolicyRejection);
        Assert.Equal("The state backend returned an error.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateAddressAsync_PropagatesBackendMessage_OnPolicyRejection()
    {
        // Arrange — a message-driven policy rejection propagates the backend's own text.
        const string policyMessage = "Policy Failure: address updates are locked for this household.";

        AddressUpdateOperationConfig operation = DcAddressUpdate() with
        {
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
                        ValueIn = new List<string> { "OK" },
                    },
                },
                Default = WriteOutcome.BackendError,
            },
        };

        var caseIds = new List<string>
        {
            OpaqueCaseId.Compose(new Dictionary<string, string>
            {
                ["writeId"] = "W-1",
                ["householdEmail"] = "family@example.test",
            }),
        };

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/households/address")
            .Respond(
                "application/json",
                $$"""{ "resultCode": "ERR", "resultMessage": "{{policyMessage}}" }""");

        var backend = BuildBackend(mockHttp, operation);

        // Act
        WriteResult result = await backend.UpdateAddressAsync(
            new AddressUpdateRequest("family@example.test", caseIds, SampleAddress()));

        // Assert
        Assert.True(result.IsPolicyRejection);
        Assert.Equal(policyMessage, result.ErrorMessage);
    }

    // ---- 3. CO body: collect per-case write-ids into an array, classified ------------------------

    [Fact]
    public async Task UpdateAddressAsync_Co_CollectsPerCaseWriteIds_IntoArray()
    {
        // Arrange
        var caseIds = new List<string>
        {
            OpaqueCaseId.Compose(new Dictionary<string, string> { ["writeId"] = "CWIN-1" }),
            OpaqueCaseId.Compose(new Dictionary<string, string> { ["writeId"] = "CWIN-2" }),
            OpaqueCaseId.Compose(new Dictionary<string, string> { ["writeId"] = "CWIN-3" }),
        };

        string? capturedBody = null;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Patch, "http://backend.test/sebt/update-std-dtls")
            .With(message =>
            {
                capturedBody = message.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return true;
            })
            .Respond("application/json", """{ "respCd": "00" }""");

        var backend = BuildBackend(mockHttp, CoAddressUpdate());
        var request = new AddressUpdateRequest("family@example.test", caseIds, SampleAddress());

        // Act
        WriteResult result = await backend.UpdateAddressAsync(request);

        // Assert — one array element per decoded caseId, in order.
        Assert.NotNull(capturedBody);
        using JsonDocument document = JsonDocument.Parse(capturedBody);
        JsonElement root = document.RootElement;

        JsonElement cases = root.GetProperty("cases");
        Assert.Equal(JsonValueKind.Array, cases.ValueKind);
        Assert.Equal(3, cases.GetArrayLength());
        Assert.Equal("CWIN-1", cases[0].GetString());
        Assert.Equal("CWIN-2", cases[1].GetString());
        Assert.Equal("CWIN-3", cases[2].GetString());

        Assert.Equal("123 Main St", root.GetProperty("stdAddr").GetString());
        Assert.Equal("20001", root.GetProperty("stdZip").GetString());

        Assert.True(result.IsSuccess);
    }

    // ---- 4. shared-disagreement fails loud -------------------------------------------------------

    [Fact]
    public async Task UpdateAddressAsync_FailsLoud_WhenSharedFieldDisagreesAcrossCaseIds()
    {
        // Arrange — two caseIds carrying different household emails: the shared field can't resolve.
        var caseIds = new List<string>
        {
            OpaqueCaseId.Compose(new Dictionary<string, string>
            {
                ["writeId"] = "W-1",
                ["householdEmail"] = "one@example.test",
            }),
            OpaqueCaseId.Compose(new Dictionary<string, string>
            {
                ["writeId"] = "W-2",
                ["householdEmail"] = "two@example.test",
            }),
        };

        var mockHttp = new MockHttpMessageHandler();
        // No backend call is registered: binding must fail loud before any request.
        var backend = BuildBackend(mockHttp, DcAddressUpdate());

        // Act + Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => backend.UpdateAddressAsync(new AddressUpdateRequest("family@example.test", caseIds, SampleAddress())));
        Assert.Contains("householdEmail", ex.Message);
    }
}
