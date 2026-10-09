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

    // Household identifier binds from the write envelope; address scalars bind via the map.
    private static AddressUpdateOperationConfig EnvelopeAddressUpdate() =>
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
                Map = new Dictionary<string, string>
                {
                    ["householdIdentifier"] = "householdIdentifier",
                    ["line1"] = "address.line1",
                    ["city"] = "address.city",
                    ["state"] = "address.state",
                    ["zip"] = "address.zip",
                },
                MapOptional = new Dictionary<string, string>
                {
                    ["line2"] = "address.line2",
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

    // One object per decoded case: routing fields plus nested address scalars.
    private static AddressUpdateOperationConfig EachCaseAddressUpdate() =>
        new()
        {
            Method = StateBackendHttpMethod.Patch,
            Path = "/sebt/update-std-dtls",
            Request = new RequestBinding
            {
                EachCase = true,
                Map = new Dictionary<string, string>
                {
                    ["sebtChldId"] = "sebtChldId",
                    ["sebtAppId"] = "sebtAppId",
                    ["line1"] = "addr.addrLn1",
                    ["city"] = "addr.cty",
                    ["state"] = "addr.staCd",
                    ["zip"] = "addr.zip",
                },
                MapOptional = new Dictionary<string, string>
                {
                    ["line2"] = "addr.addrLn2",
                    ["zip4"] = "addr.zip4",
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


    // Token-shared household field: used to pin the disagreement fail-loud path.
    private static AddressUpdateOperationConfig SharedHouseholdAddressUpdate() =>
        EnvelopeAddressUpdate() with
        {
            Request = EnvelopeAddressUpdate().Request! with
            {
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
        };

    private static AddressUpdateAddress SampleAddress() =>
        new()
        {
            Line1 = "123 Main St",
            City = "Springfield",
            State = "IL",
            Zip = "62701",
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

    // ---- 2. envelope body: household identifier + address scalars, classified -------------------

    [Fact]
    public async Task UpdateAddressAsync_EnvelopeBinding_BuildsBodyFromHouseholdIdentifierAndAddressScalars()
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

        var backend = BuildBackend(mockHttp, EnvelopeAddressUpdate());
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
        Assert.False(address.TryGetProperty("line2", out _));
        Assert.Equal("Springfield", address.GetProperty("city").GetString());
        Assert.Equal("IL", address.GetProperty("state").GetString());
        Assert.Equal("62701", address.GetProperty("zip").GetString());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateAddressAsync_EnvelopeBinding_BindsLine2_WhenPresent()
    {
        var caseIds = new List<string>
        {
            OpaqueCaseId.Compose(new Dictionary<string, string>
            {
                ["writeId"] = "W-1",
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

        var backend = BuildBackend(mockHttp, EnvelopeAddressUpdate());
        var request = new AddressUpdateRequest(
            "IC10001",
            caseIds,
            SampleAddress() with { Line2 = "Apt 4B" });

        WriteResult result = await backend.UpdateAddressAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedBody);
        using JsonDocument document = JsonDocument.Parse(capturedBody);
        Assert.Equal(
            "Apt 4B",
            document.RootElement.GetProperty("address").GetProperty("line2").GetString());
    }

    [Fact]
    public async Task UpdateAddressAsync_EnvelopeBinding_ClassifiesBackendError_ByDefault()
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

        var backend = BuildBackend(mockHttp, EnvelopeAddressUpdate());

        // Act
        WriteResult result = await backend.UpdateAddressAsync(
            new AddressUpdateRequest("family@example.test", caseIds, SampleAddress()));

        // Assert — nothing matches → default BackendError. This classifier declares no
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

        AddressUpdateOperationConfig operation = EnvelopeAddressUpdate() with
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

    [Fact]
    public async Task UpdateAddressAsync_EachCase_SendsOneObjectPerCase()
    {
        // Arrange
        var caseIds = new List<string>
        {
            OpaqueCaseId.Compose(new Dictionary<string, string>
            {
                ["sebtChldId"] = "1301305",
                ["sebtAppId"] = "1299750",
            }),
            OpaqueCaseId.Compose(new Dictionary<string, string>
            {
                ["sebtChldId"] = "1301306",
                ["sebtAppId"] = "1299751",
            }),
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

        var backend = BuildBackend(mockHttp, EachCaseAddressUpdate());
        var request = new AddressUpdateRequest(
            "family@example.test",
            caseIds,
            SampleAddress() with { Line2 = "Floor 2", Zip = "62701-6789" });

        // Act
        WriteResult result = await backend.UpdateAddressAsync(request);

        // Assert — a JSON array, one object per case
        Assert.NotNull(capturedBody);
        using JsonDocument document = JsonDocument.Parse(capturedBody);
        JsonElement root = document.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("1301305", root[0].GetProperty("sebtChldId").GetString());
        Assert.Equal("1299750", root[0].GetProperty("sebtAppId").GetString());
        JsonElement address = root[0].GetProperty("addr");
        Assert.Equal("123 Main St", address.GetProperty("addrLn1").GetString());
        Assert.Equal("Floor 2", address.GetProperty("addrLn2").GetString());
        Assert.Equal("Springfield", address.GetProperty("cty").GetString());
        Assert.Equal("IL", address.GetProperty("staCd").GetString());
        Assert.Equal("62701", address.GetProperty("zip").GetString());
        Assert.Equal("6789", address.GetProperty("zip4").GetString());
        Assert.Equal("1301306", root[1].GetProperty("sebtChldId").GetString());

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
        var backend = BuildBackend(mockHttp, SharedHouseholdAddressUpdate());

        // Act + Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => backend.UpdateAddressAsync(new AddressUpdateRequest("family@example.test", caseIds, SampleAddress())));
        Assert.Contains("householdEmail", ex.Message);
    }

    [Fact]
    public async Task UpdateAddressAsync_EnvelopeBinding_AllowsEmptyCaseIds()
    {
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

        var backend = BuildBackend(mockHttp, EnvelopeAddressUpdate());

        WriteResult result = await backend.UpdateAddressAsync(
            new AddressUpdateRequest("family@example.test", Array.Empty<string>(), SampleAddress()));

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedBody);
        using JsonDocument document = JsonDocument.Parse(capturedBody);
        Assert.Equal("family@example.test", document.RootElement.GetProperty("householdIdentifier").GetString());
    }
}
