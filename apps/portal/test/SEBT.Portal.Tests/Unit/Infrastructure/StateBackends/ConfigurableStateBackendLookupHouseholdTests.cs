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
            new[] { new IdentitySignal("email", "ada@example.test", Verified: true) });

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
    private const string DcTypedResponse =
        """
        {
          "resultSets": [
            [
              {
                "SummerEBTCaseID": "SEBT-001",
                "ChildFirstName": "Ada",
                "ChildLastName": "Lovelace",
                "IssueDate": "06/15/2025",
                "CardStatus": "LOST, AUTO REISSUE"
              }
            ]
          ]
        }
        """;

    [Fact]
    public async Task LookupHouseholdAsync_MapsTypedFields_StringsDateAndEnum()
    {
        // Arrange
        StateBackendConfiguration configuration = BuildConfiguration();
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
            Operations = configuration.Operations with
            {
                HouseholdLookup = configuration.Operations.HouseholdLookup! with
                {
                    Response = new StateBackendResponseMapping
                    {
                        Root = "$.resultSets[0]",
                        Fields = new Dictionary<string, FieldMapping>
                        {
                            ["summerEBTCaseID"] = new() { From = "SummerEBTCaseID" },
                            ["childFirstName"] = new() { From = "ChildFirstName" },
                            ["ebtCardIssueDate"] = new() { From = "IssueDate", Format = "MM/dd/yyyy" },
                            ["ebtCardStatus"] = new() { From = "CardStatus", Enum = "cardStatus" },
                        },
                    },
                },
            },
        };

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .Expect(HttpMethod.Post, "http://backend.test/households/lookup")
            .Respond("application/json", DcTypedResponse);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(configuration, httpClient);
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("email", "ada@example.test", Verified: true) });

        // Act
        HouseholdLookupResult result = await backend.LookupHouseholdAsync(request);

        // Assert
        Assert.Equal(HouseholdLookupStatus.Found, result.Status);
        SummerEbtCase mapped = Assert.Single(result.Household!.SummerEbtCases);

        Assert.Equal("SEBT-001", mapped.SummerEBTCaseID);
        Assert.Equal("Ada", mapped.ChildFirstName);

        // Date parsed exactly with the configured format.
        Assert.Equal(new DateTime(2025, 6, 15), mapped.EbtCardIssueDate);

        // "LOST, AUTO REISSUE" resolves through the domain-centered table to CardStatus.Lost.
        Assert.Equal(CardStatus.Lost, mapped.EbtCardStatus);

        mockHttp.VerifyNoOutstandingExpectation();
    }

    // Domain-centered DC request binding: constants + map (named input → dotted target path).
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
        return configuration with
        {
            Operations = configuration.Operations with
            {
                HouseholdLookup = configuration.Operations.HouseholdLookup! with
                {
                    Request = binding,
                },
            },
        };
    }

    private static HouseholdLookupRequest DcRequest(bool isProofed) =>
        new(
            new[]
            {
                new IdentitySignal("email", "ada@example.test", Verified: true),
                new IdentitySignal("ic", "IC-123", Verified: true),
                new IdentitySignal("dob", "1815-12-10", Verified: true),
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

        // Constant, value false (genuinely fixed policy — production always sends false).
        Assert.False(root.GetProperty("includePendingApplicantDetails").GetBoolean());

        // Signal → top-level target path.
        Assert.Equal("ada@example.test", root.GetProperty("guardianEmail").GetString());

        // Signals + context nested via dotted target paths.
        JsonElement guardianIdentifiers = root.GetProperty("guardianIdentifiers");
        Assert.Equal("IC-123", guardianIdentifiers.GetProperty("IC").GetString());
        Assert.Equal("1815-12-10", guardianIdentifiers.GetProperty("DOB").GetString());
        Assert.Equal("uuid-abc", guardianIdentifiers.GetProperty("PortalUUID").GetString());
    }

    // SECURITY REGRESSION: isIdentityProofed must reflect the caller's real proofing status, not a
    // hardcoded value. The DC sproc gates its email-lookup branch on @isIdentityProofed = 1;
    // hardcoding true would be a proofing-gate bypass.
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

        // Assert — body mirrors the caller's proofing, in both directions.
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
            new[] { new IdentitySignal("email", "ada@example.test", Verified: true) });

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
            new[] { new IdentitySignal("phone", "5551234567", Verified: true) });

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
        configuration = configuration with
        {
            Operations = configuration.Operations with
            {
                HouseholdLookup = configuration.Operations.HouseholdLookup! with
                {
                    Response = configuration.Operations.HouseholdLookup!.Response! with
                    {
                        Disaggregation = new StateBackendDisaggregation
                        {
                            Rule = DisaggregationRule.Presence,
                            DiscriminatorField = "ApplicationId",
                            GroupApplicationsBy = "ApplicationId",
                            CaseInclusion = CaseInclusionPredicate.All,
                        },
                    },
                },
            },
        };

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .Expect(HttpMethod.Post, "http://backend.test/households/lookup")
            .Respond("application/json", DcDisaggregationResponse);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(configuration, httpClient);
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("email", "ada@example.test", Verified: true) });

        // Act
        HouseholdLookupResult result = await backend.LookupHouseholdAsync(request);

        // Assert
        Assert.Equal(HouseholdLookupStatus.Found, result.Status);
        Assert.NotNull(result.Household);

        // CaseInclusion.All: every record yields a case.
        List<SummerEbtCase> cases = result.Household.SummerEbtCases;
        Assert.Equal(3, cases.Count);

        // Application-based cases link to their application; the auto-issued case does not.
        Assert.Equal("APP-100", cases[0].ApplicationId);
        Assert.Equal("APP-100", cases[1].ApplicationId);
        Assert.Null(cases[2].ApplicationId);

        // Two application-based records grouped by ApplicationId → one application.
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
        StateBackendConfiguration configuration = BuildConfiguration();
        configuration = configuration with
        {
            Operations = configuration.Operations with
            {
                HouseholdLookup = configuration.Operations.HouseholdLookup! with
                {
                    Response = new StateBackendResponseMapping
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
                    },
                },
            },
        };

        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .Expect(HttpMethod.Post, "http://backend.test/households/lookup")
            .Respond("application/json", CoDisaggregationResponse);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(configuration, httpClient);
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("phone", "5551234567", Verified: true) });

        // Act
        HouseholdLookupResult result = await backend.LookupHouseholdAsync(request);

        // Assert
        Assert.Equal(HouseholdLookupStatus.Found, result.Status);
        Assert.NotNull(result.Household);

        // CaseInclusion.All: every record yields a case.
        List<SummerEbtCase> cases = result.Household.SummerEbtCases;
        Assert.Equal(3, cases.Count);

        // eligSrc in {CBMS,PK} → application-based, linked to sebtAppId. SCHOOL → streamlined, unlinked.
        Assert.Equal("APP-CO-1", cases[0].ApplicationId);
        Assert.Equal("APP-CO-1", cases[1].ApplicationId);
        Assert.Null(cases[2].ApplicationId);

        // Two application-based records grouped by sebtAppId → one application.
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
        StateBackendConfiguration configuration = BuildConfiguration();
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
            Operations = configuration.Operations with
            {
                HouseholdLookup = configuration.Operations.HouseholdLookup! with
                {
                    Response = new StateBackendResponseMapping
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
                    },
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
            new[] { new IdentitySignal("phone", "5551234567", Verified: true) });

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

        // The denied record is still part of the (pending) application it belongs to.
        List<Application> applications = result.Household.Applications;
        Assert.Single(applications);
        Assert.Equal("APP-CO-1", applications[0].ApplicationNumber);

        mockHttp.VerifyNoOutstandingExpectation();
    }

    [Fact]
    public async Task LookupHouseholdAsync_ReturnsNotFound_WhenRootSelectsNoRecords()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/households/lookup")
            .Respond("application/json", "{ \"resultSets\": [ [] ] }");

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(BuildConfiguration(), httpClient);
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("email", "nobody@example.test", Verified: true) });

        // Act
        HouseholdLookupResult result = await backend.LookupHouseholdAsync(request);

        // Assert
        Assert.Equal(HouseholdLookupStatus.NotFound, result.Status);
        Assert.Null(result.Household);
    }
}
