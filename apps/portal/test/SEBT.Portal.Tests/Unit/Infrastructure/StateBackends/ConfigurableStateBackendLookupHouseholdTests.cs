using System.Net;
using System.Text.Json;
using RichardSzalay.MockHttp;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

public class ConfigurableStateBackendLookupHouseholdTests
{
    private static StateBackendConfiguration BuildConfiguration() =>
        new()
        {
            BaseUrl = new Uri("http://backend.test"),
            Auth = new StateBackendApiKeyAuthScheme
            {
                Header = "X-Api-Key",
                KeyRef = "dc-api-key",
            },
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
                            ["summerEBTCaseID"] = new() { From = "SummerEBTCaseID" },
                            ["childFirstName"] = new() { From = "ChildFirstName" },
                            ["childLastName"] = new() { From = "ChildLastName" },
                        },
                    },
                },
            },
        };

    // Rebuilds the config with its household-lookup operation swapped out.
    private static StateBackendConfiguration WithLookup(
        StateBackendConfiguration configuration, HouseholdLookupOperationConfig lookup) =>
        configuration with
        {
            Operations = configuration.Operations with { HouseholdLookup = lookup },
        };

    // Rebuilds the config with its household-lookup response mapping swapped out.
    private static StateBackendConfiguration WithLookupResponse(
        StateBackendConfiguration configuration, StateBackendResponseMapping response) =>
        WithLookup(configuration, configuration.Operations.HouseholdLookup! with { Response = response });

    // Raw passthrough shaped like the DC REST wrapper: multi-result-set, original column names.
    private const string DcRawResponse =
        """
        {
          "resultSets": [
            [
              { "SummerEBTCaseID": "SEBT-001", "ChildFirstName": "Ada", "ChildLastName": "Lovelace", "ApplicationId": null },
              { "SummerEBTCaseID": "SEBT-002", "ChildFirstName": "Alan", "ChildLastName": "Turing", "ApplicationId": null }
            ]
          ]
        }
        """;

    [Fact]
    public async Task LookupHouseholdAsync_MapsRootAndFields_IntoCanonicalCases()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .Expect(HttpMethod.Post, "http://backend.test/households/lookup")
            .Respond("application/json", DcRawResponse);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(BuildConfiguration(), httpClient);
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("email", "ada@example.test") });

        // Act
        HouseholdLookupResult result = await backend.LookupHouseholdAsync(request);

        // Assert
        Assert.Equal(HouseholdLookupStatus.Found, result.Status);
        Assert.NotNull(result.Household);

        List<SummerEbtCase> cases = result.Household.SummerEbtCases;
        Assert.Equal(2, cases.Count);

        Assert.Equal("SEBT-001", cases[0].SummerEBTCaseID);
        Assert.Equal("Ada", cases[0].ChildFirstName);
        Assert.Equal("Lovelace", cases[0].ChildLastName);

        Assert.Equal("SEBT-002", cases[1].SummerEBTCaseID);
        Assert.Equal("Alan", cases[1].ChildFirstName);
        Assert.Equal("Turing", cases[1].ChildLastName);

        mockHttp.VerifyNoOutstandingExpectation();
    }

    // DC-shaped record carrying a formatted date and a state card-status token.
    private static string DcTypedResponse(string cardStatusToken) =>
        $$"""
        {
          "resultSets": [
            [
              {
                "SummerEBTCaseID": "SEBT-001",
                "ChildFirstName": "Ada",
                "ChildLastName": "Lovelace",
                "IssueDate": "06/15/2025",
                "CardStatus": "{{cardStatusToken}}"
              }
            ]
          ]
        }
        """;

    [Theory]
    [InlineData("ACTIVE", CardStatus.Active)]
    // "LOST, AUTO REISSUE" is one of several tokens the table maps to CardStatus.Lost.
    [InlineData("LOST, AUTO REISSUE", CardStatus.Lost)]
    // An unmapped token falls through to the enum table's default.
    [InlineData("NEVER SEEN", CardStatus.Unknown)]
    public async Task LookupHouseholdAsync_MapsTypedFields_StringsDateAndEnum(
        string cardStatusToken, CardStatus expectedCardStatus)
    {
        // Arrange
        StateBackendConfiguration configuration = WithLookupResponse(
            BuildConfiguration(),
            new StateBackendResponseMapping
            {
                Root = "$.resultSets[0]",
                Fields = new Dictionary<string, FieldMapping>
                {
                    ["summerEBTCaseID"] = new() { From = "SummerEBTCaseID" },
                    ["childFirstName"] = new() { From = "ChildFirstName" },
                    ["ebtCardIssueDate"] = new() { From = "IssueDate", Format = "MM/dd/yyyy" },
                    ["ebtCardStatus"] = new() { From = "CardStatus", Enum = "cardStatus" },
                },
            });
        configuration = configuration with
        {
            Enums = new Dictionary<string, StateBackendEnumTable>
            {
                ["cardStatus"] = new()
                {
                    Map = new Dictionary<string, List<string>>
                    {
                        ["Active"] = new() { "ACTIVE" },
                        ["Lost"] = new() { "LOST", "LOST, AUTO REISSUE" },
                    },
                    Default = "Unknown",
                },
            },
        };

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .Expect(HttpMethod.Post, "http://backend.test/households/lookup")
            .Respond("application/json", DcTypedResponse(cardStatusToken));

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(configuration, httpClient);
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("email", "ada@example.test") });

        // Act
        HouseholdLookupResult result = await backend.LookupHouseholdAsync(request);

        // Assert
        Assert.Equal(HouseholdLookupStatus.Found, result.Status);
        SummerEbtCase mapped = Assert.Single(result.Household!.SummerEbtCases);

        Assert.Equal("SEBT-001", mapped.SummerEBTCaseID);
        Assert.Equal("Ada", mapped.ChildFirstName);

        Assert.Equal(new DateTime(2025, 6, 15), mapped.EbtCardIssueDate);

        Assert.Equal(expectedCardStatus, mapped.EbtCardStatus);

        mockHttp.VerifyNoOutstandingExpectation();
    }

    private static RequestBinding DcRequestBinding() =>
        new()
        {
            Constants = new Dictionary<string, object>
            {
                ["includePendingApplicantDetails"] = false,
            },
            Map = new Dictionary<string, string>
            {
                ["email"] = "guardianEmail",
                ["ic"] = "guardianIdentifiers.IC",
                ["dob"] = "guardianIdentifiers.DOB",
                ["portalUuid"] = "guardianIdentifiers.PortalUUID",
                ["isProofed"] = "isIdentityProofed",
            },
        };

    private static StateBackendConfiguration WithRequestBinding(RequestBinding binding)
    {
        StateBackendConfiguration configuration = BuildConfiguration();
        return WithLookup(
            configuration, configuration.Operations.HouseholdLookup! with { Request = binding });
    }

    private static HouseholdLookupRequest DcRequest(bool isProofed) =>
        new(
            new[]
            {
                new IdentitySignal("email", "ada@example.test"),
                new IdentitySignal("ic", "IC-123"),
                new IdentitySignal("dob", "1815-12-10"),
            })
        {
            IsProofed = isProofed,
            PortalUuid = "uuid-abc",
        };

    private static async Task<string> CaptureLookupBodyAsync(
        StateBackendConfiguration configuration, HouseholdLookupRequest request)
    {
        string? capturedBody = null;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/households/lookup")
            .With(message =>
            {
                capturedBody = message.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return true;
            })
            .Respond("application/json", DcRawResponse);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(configuration, httpClient);
        await backend.LookupHouseholdAsync(request);

        Assert.NotNull(capturedBody);
        return capturedBody;
    }

    [Fact]
    public async Task LookupHouseholdAsync_BindsDcRequestBody_FromSignalsContextAndConstants()
    {
        // Arrange
        StateBackendConfiguration configuration = WithRequestBinding(DcRequestBinding());

        // Act
        string capturedBody = await CaptureLookupBodyAsync(configuration, DcRequest(isProofed: true));

        // Assert
        using JsonDocument document = JsonDocument.Parse(capturedBody);
        JsonElement root = document.RootElement;

        Assert.False(root.GetProperty("includePendingApplicantDetails").GetBoolean());
        Assert.Equal("ada@example.test", root.GetProperty("guardianEmail").GetString());

        // Dotted target paths produce nested objects.
        JsonElement guardianIdentifiers = root.GetProperty("guardianIdentifiers");
        Assert.Equal("IC-123", guardianIdentifiers.GetProperty("IC").GetString());
        Assert.Equal("1815-12-10", guardianIdentifiers.GetProperty("DOB").GetString());
        Assert.Equal("uuid-abc", guardianIdentifiers.GetProperty("PortalUUID").GetString());
    }

    // SECURITY: isIdentityProofed must mirror the caller's real proofing status. The DC lookup gates
    // its email branch on it; hardcoding true would bypass the proofing gate.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LookupHouseholdAsync_BindsIsIdentityProofed_FromCallerProofingStatus(
        bool isProofed)
    {
        // Arrange
        StateBackendConfiguration configuration = WithRequestBinding(DcRequestBinding());

        // Act
        string capturedBody = await CaptureLookupBodyAsync(
            configuration, DcRequest(isProofed: isProofed));

        // Assert
        using JsonDocument document = JsonDocument.Parse(capturedBody);
        Assert.Equal(
            isProofed, document.RootElement.GetProperty("isIdentityProofed").GetBoolean());
    }

    // Fail-loud: a map entry whose input isn't present must throw, not silently drop.
    [Fact]
    public async Task LookupHouseholdAsync_ThrowsWhenMapInputIsNotPresent()
    {
        // Arrange — map requires an "ic" signal that the request does not carry.
        StateBackendConfiguration configuration = WithRequestBinding(
            new RequestBinding
            {
                Map = new Dictionary<string, string>
                {
                    ["ic"] = "guardianIdentifiers.IC",
                },
            });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/households/lookup")
            .Respond("application/json", DcRawResponse);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(configuration, httpClient);
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("email", "ada@example.test") });

        // Act + Assert
        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => backend.LookupHouseholdAsync(request));
        Assert.Contains("ic", ex.Message);
    }

    [Fact]
    public async Task LookupHouseholdAsync_BindsCoRequestBody_FromPhoneSignal()
    {
        // Arrange
        StateBackendConfiguration configuration = WithRequestBinding(
            new RequestBinding
            {
                Map = new Dictionary<string, string>
                {
                    ["phone"] = "PhnNm",
                },
            });
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("phone", "5551234567") });

        // Act
        string capturedBody = await CaptureLookupBodyAsync(configuration, request);

        // Assert
        using JsonDocument document = JsonDocument.Parse(capturedBody);
        Assert.Equal("5551234567", document.RootElement.GetProperty("PhnNm").GetString());
    }

    // DC-shaped mix: two records carry an ApplicationId (same one → one grouped application),
    // one does not (auto-issued case, no application).
    private const string DcDisaggregationResponse =
        """
        {
          "resultSets": [
            [
              { "SummerEBTCaseID": "SEBT-001", "ChildFirstName": "Ada", "ChildLastName": "Lovelace", "ApplicationId": "APP-100" },
              { "SummerEBTCaseID": "SEBT-002", "ChildFirstName": "Alan", "ChildLastName": "Turing", "ApplicationId": "APP-100" },
              { "SummerEBTCaseID": "SEBT-003", "ChildFirstName": "Grace", "ChildLastName": "Hopper", "ApplicationId": null }
            ]
          ]
        }
        """;

    [Fact]
    public async Task LookupHouseholdAsync_PresenceRule_All_SplitsCasesAndGroupsApplications()
    {
        // Arrange
        StateBackendConfiguration configuration = BuildConfiguration();
        configuration = WithLookupResponse(
            configuration,
            configuration.Operations.HouseholdLookup!.Response! with
            {
                Disaggregation = new StateBackendDisaggregation
                {
                    Rule = DisaggregationRule.Presence,
                    DiscriminatorField = "ApplicationId",
                    GroupApplicationsBy = "ApplicationId",
                    CaseInclusion = CaseInclusionPredicate.All,
                },
            });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .Expect(HttpMethod.Post, "http://backend.test/households/lookup")
            .Respond("application/json", DcDisaggregationResponse);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(configuration, httpClient);
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("email", "ada@example.test") });

        // Act
        HouseholdLookupResult result = await backend.LookupHouseholdAsync(request);

        // Assert
        Assert.Equal(HouseholdLookupStatus.Found, result.Status);
        Assert.NotNull(result.Household);

        List<SummerEbtCase> cases = result.Household.SummerEbtCases;
        Assert.Equal(3, cases.Count);

        // App-based cases link to their application; the auto-issued case does not.
        Assert.Equal("APP-100", cases[0].ApplicationId);
        Assert.Equal("APP-100", cases[1].ApplicationId);
        Assert.Null(cases[2].ApplicationId);

        List<Application> applications = result.Household.Applications;
        Assert.Single(applications);
        Assert.Equal("APP-100", applications[0].ApplicationNumber);

        mockHttp.VerifyNoOutstandingExpectation();
    }

    // CO-shaped: eligSrc partitions application-based (CBMS/PK) from streamlined (SCHOOL).
    // Two application-based records share sebtAppId APP-CO-1 → one grouped application.
    private const string CoDisaggregationResponse =
        """
        {
          "stdntEnrollDtls": [
            { "sebtChldCwin": "CO-001", "chldNm": "Ada", "eligSrc": "CBMS", "sebtAppId": "APP-CO-1" },
            { "sebtChldCwin": "CO-002", "chldNm": "Alan", "eligSrc": "PK", "sebtAppId": "APP-CO-1" },
            { "sebtChldCwin": "CO-003", "chldNm": "Grace", "eligSrc": "SCHOOL", "sebtAppId": null }
          ]
        }
        """;

    [Fact]
    public async Task LookupHouseholdAsync_ValueInSetRule_All_PartitionsAndGroupsApplications()
    {
        // Arrange
        StateBackendConfiguration configuration = WithLookupResponse(
            BuildConfiguration(),
            new StateBackendResponseMapping
            {
                Root = "$.stdntEnrollDtls",
                Fields = new Dictionary<string, FieldMapping>
                {
                    ["summerEBTCaseID"] = new() { From = "sebtChldCwin" },
                    ["childFirstName"] = new() { From = "chldNm" },
                },
                Disaggregation = new StateBackendDisaggregation
                {
                    Rule = DisaggregationRule.ValueInSet,
                    DiscriminatorField = "eligSrc",
                    ApplicationValues = new List<string> { "CBMS", "PK" },
                    GroupApplicationsBy = "sebtAppId",
                    CaseInclusion = CaseInclusionPredicate.All,
                },
            });

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .Expect(HttpMethod.Post, "http://backend.test/households/lookup")
            .Respond("application/json", CoDisaggregationResponse);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(configuration, httpClient);
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("phone", "5551234567") });

        // Act
        HouseholdLookupResult result = await backend.LookupHouseholdAsync(request);

        // Assert
        Assert.Equal(HouseholdLookupStatus.Found, result.Status);
        Assert.NotNull(result.Household);

        List<SummerEbtCase> cases = result.Household.SummerEbtCases;
        Assert.Equal(3, cases.Count);

        // eligSrc in {CBMS,PK} → app-based, linked to sebtAppId; SCHOOL → streamlined, unlinked.
        Assert.Equal("APP-CO-1", cases[0].ApplicationId);
        Assert.Equal("APP-CO-1", cases[1].ApplicationId);
        Assert.Null(cases[2].ApplicationId);

        List<Application> applications = result.Household.Applications;
        Assert.Single(applications);
        Assert.Equal("APP-CO-1", applications[0].ApplicationNumber);

        mockHttp.VerifyNoOutstandingExpectation();
    }

    // CO-shaped with an application status: two app-based records (CBMS/PK) share sebtAppId
    // APP-CO-1 — one Approved (AP), one Denied (DE) — plus one streamlined (SCHOOL) record.
    private const string CoStatusDisaggregationResponse =
        """
        {
          "stdntEnrollDtls": [
            { "sebtChldCwin": "CO-001", "chldNm": "Ada", "eligSrc": "CBMS", "sebtAppId": "APP-CO-1", "sebtAppSts": "AP" },
            { "sebtChldCwin": "CO-002", "chldNm": "Alan", "eligSrc": "PK", "sebtAppId": "APP-CO-1", "sebtAppSts": "DE" },
            { "sebtChldCwin": "CO-003", "chldNm": "Grace", "eligSrc": "SCHOOL", "sebtAppId": null, "sebtAppSts": null }
          ]
        }
        """;

    private static StateBackendConfiguration WithCoStatusInclusion()
    {
        StateBackendConfiguration configuration = WithLookupResponse(
            BuildConfiguration(),
            new StateBackendResponseMapping
            {
                Root = "$.stdntEnrollDtls",
                Fields = new Dictionary<string, FieldMapping>
                {
                    ["summerEBTCaseID"] = new() { From = "sebtChldCwin" },
                    ["childFirstName"] = new() { From = "chldNm" },
                    ["applicationStatus"] = new() { From = "sebtAppSts", Enum = "applicationStatus" },
                },
                Disaggregation = new StateBackendDisaggregation
                {
                    Rule = DisaggregationRule.ValueInSet,
                    DiscriminatorField = "eligSrc",
                    ApplicationValues = new List<string> { "CBMS", "PK" },
                    GroupApplicationsBy = "sebtAppId",
                    CaseInclusion = CaseInclusionPredicate.WhenApprovedOrNotApplicationBased,
                },
            });
        return configuration with
        {
            Enums = new Dictionary<string, StateBackendEnumTable>
            {
                ["applicationStatus"] = new()
                {
                    Map = new Dictionary<string, List<string>>
                    {
                        ["Approved"] = new() { "AP" },
                        ["Denied"] = new() { "DE" },
                        ["Pending"] = new() { "PD", "PE" },
                    },
                    Default = "Unknown",
                },
            },
        };
    }

    [Fact]
    public async Task LookupHouseholdAsync_WhenApprovedOrNotApplicationBased_IncludesApprovedAndStreamlinedCases()
    {
        // Arrange
        StateBackendConfiguration configuration = WithCoStatusInclusion();

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .Expect(HttpMethod.Post, "http://backend.test/households/lookup")
            .Respond("application/json", CoStatusDisaggregationResponse);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(configuration, httpClient);
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("phone", "5551234567") });

        // Act
        HouseholdLookupResult result = await backend.LookupHouseholdAsync(request);

        // Assert
        Assert.Equal(HouseholdLookupStatus.Found, result.Status);
        Assert.NotNull(result.Household);

        // Cases: app-based+Approved (CO-001) and streamlined (CO-003) are included.
        // The app-based+Denied record (CO-002) is NOT a case.
        List<SummerEbtCase> cases = result.Household.SummerEbtCases;
        Assert.Equal(2, cases.Count);
        Assert.Equal("CO-001", cases[0].SummerEBTCaseID);
        Assert.Equal(ApplicationStatus.Approved, cases[0].ApplicationStatus);
        Assert.Equal("APP-CO-1", cases[0].ApplicationId);

        Assert.Equal("CO-003", cases[1].SummerEBTCaseID);
        Assert.Null(cases[1].ApplicationId);

        // The denied record is excluded as a case but still belongs to its (pending) application.
        List<Application> applications = result.Household.Applications;
        Assert.Single(applications);
        Assert.Equal("APP-CO-1", applications[0].ApplicationNumber);

        mockHttp.VerifyNoOutstandingExpectation();
    }

    // DC-shaped free-text records exercising issuance-type inference, including one carrying both an
    // OSSE and a SNAP keyword (first-match-wins tiebreak) and one with no keyword (default).
    private const string DcIssuanceResponse =
        """
        {
          "resultSets": [
            [
              { "SummerEBTCaseID": "SEBT-001", "HouseholdType": "OSSE ENROLLED", "EligibilityType": "DIRECT CERT" },
              { "SummerEBTCaseID": "SEBT-002", "HouseholdType": "STANDARD", "EligibilityType": "SNAP HOUSEHOLD" },
              { "SummerEBTCaseID": "SEBT-003", "HouseholdType": "OSSE ENROLLED", "EligibilityType": "SNAP HOUSEHOLD" },
              { "SummerEBTCaseID": "SEBT-004", "HouseholdType": "STANDARD", "EligibilityType": "NONE" }
            ]
          ]
        }
        """;

    private static StateBackendConfiguration WithIssuanceKeywordRules() =>
        WithLookupResponse(
            BuildConfiguration(),
            new StateBackendResponseMapping
            {
                Root = "$.resultSets[0]",
                Fields = new Dictionary<string, FieldMapping>
                {
                    ["summerEBTCaseID"] = new() { From = "SummerEBTCaseID" },
                    ["issuanceType"] = new()
                    {
                        From = new[] { "HouseholdType", "EligibilityType" },
                        KeywordRules = new KeywordRules
                        {
                            // Order is load-bearing: SummerEbt is evaluated before SnapEbtCard.
                            Order = new List<string> { "SummerEbt", "SnapEbtCard", "TanfEbtCard" },
                            Map = new Dictionary<string, List<string>>
                            {
                                ["SummerEbt"] = new() { "OSSE", "NSLP" },
                                ["SnapEbtCard"] = new() { "FOOD", "SNAP" },
                                ["TanfEbtCard"] = new() { "CASH", "TANF" },
                            },
                            Default = "Unknown",
                        },
                    },
                },
            });

    [Fact]
    public async Task LookupHouseholdAsync_InfersIssuanceType_FromKeywordRules_FirstMatchWins()
    {
        // Arrange
        StateBackendConfiguration configuration = WithIssuanceKeywordRules();

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .Expect(HttpMethod.Post, "http://backend.test/households/lookup")
            .Respond("application/json", DcIssuanceResponse);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(configuration, httpClient);
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("email", "ada@example.test") });

        // Act
        HouseholdLookupResult result = await backend.LookupHouseholdAsync(request);

        // Assert
        Assert.Equal(HouseholdLookupStatus.Found, result.Status);
        List<SummerEbtCase> cases = result.Household!.SummerEbtCases;
        Assert.Equal(4, cases.Count);

        Assert.Equal(IssuanceType.SummerEbt, cases[0].IssuanceType);
        Assert.Equal(IssuanceType.SnapEbtCard, cases[1].IssuanceType);

        // Both OSSE and SNAP present → SummerEbt wins (earlier in `order`).
        Assert.Equal(IssuanceType.SummerEbt, cases[2].IssuanceType);

        // No keyword → default.
        Assert.Equal(IssuanceType.Unknown, cases[3].IssuanceType);

        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Theory]
    [InlineData("""{ "resultSets": [ [] ] }""")] // root resolves to an empty array
    [InlineData("{}")] // root path missing entirely — the selector finds nothing to map
    public async Task LookupHouseholdAsync_ReturnsNotFound_WhenRootSelectsNoRecords(string responseJson)
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/households/lookup")
            .Respond("application/json", responseJson);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(BuildConfiguration(), httpClient);
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("email", "nobody@example.test") });

        // Act
        HouseholdLookupResult result = await backend.LookupHouseholdAsync(request);

        // Assert
        Assert.Equal(HouseholdLookupStatus.NotFound, result.Status);
        Assert.Null(result.Household);
    }
}
