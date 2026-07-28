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
                        Fields = new Dictionary<string, string>
                        {
                            ["summerEBTCaseID"] = "SummerEBTCaseID",
                            ["childFirstName"] = "ChildFirstName",
                            ["childLastName"] = "ChildLastName",
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

    [Fact]
    public async Task LookupHouseholdAsync_BindsDcRequestBody_FromSignals()
    {
        // Arrange
        StateBackendConfiguration configuration = BuildConfiguration();
        configuration = configuration with
        {
            Operations = configuration.Operations with
            {
                HouseholdLookup = configuration.Operations.HouseholdLookup! with
                {
                    Request = new Dictionary<string, RequestBinding>
                    {
                        ["isIdentityProofed"] = new RequestBinding { Const = true },
                        ["includePendingApplicantDetails"] = new RequestBinding { Const = true },
                        ["guardianEmail"] = new RequestBinding { From = "email" },
                        ["guardianIdentifiers"] = new RequestBinding
                        {
                            Compose = new Dictionary<string, RequestBinding>
                            {
                                ["IC"] = new RequestBinding { From = "ic" },
                                ["DOB"] = new RequestBinding { From = "dob" },
                                ["PortalUUID"] = new RequestBinding { From = "portalUuid" },
                            },
                        },
                    },
                },
            },
        };

        string? capturedBody = null;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/households/lookup")
            .With(request =>
            {
                capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return true;
            })
            .Respond("application/json", DcRawResponse);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(configuration, httpClient);
        var request = new HouseholdLookupRequest(
            new[]
            {
                new IdentitySignal("email", "ada@example.test", Verified: true),
                new IdentitySignal("ic", "IC-123", Verified: true),
                new IdentitySignal("dob", "1815-12-10", Verified: true),
                new IdentitySignal("portalUuid", "uuid-abc", Verified: false),
            });

        // Act
        await backend.LookupHouseholdAsync(request);

        // Assert
        Assert.NotNull(capturedBody);
        using JsonDocument document = JsonDocument.Parse(capturedBody);
        JsonElement root = document.RootElement;

        Assert.True(root.GetProperty("isIdentityProofed").GetBoolean());
        Assert.True(root.GetProperty("includePendingApplicantDetails").GetBoolean());
        Assert.Equal("ada@example.test", root.GetProperty("guardianEmail").GetString());

        JsonElement guardianIdentifiers = root.GetProperty("guardianIdentifiers");
        Assert.Equal("IC-123", guardianIdentifiers.GetProperty("IC").GetString());
        Assert.Equal("1815-12-10", guardianIdentifiers.GetProperty("DOB").GetString());
        Assert.Equal("uuid-abc", guardianIdentifiers.GetProperty("PortalUUID").GetString());
    }

    [Fact]
    public async Task LookupHouseholdAsync_BindsCoRequestBody_FromPhoneSignal()
    {
        // Arrange
        StateBackendConfiguration configuration = BuildConfiguration();
        configuration = configuration with
        {
            Operations = configuration.Operations with
            {
                HouseholdLookup = configuration.Operations.HouseholdLookup! with
                {
                    Request = new Dictionary<string, RequestBinding>
                    {
                        ["PhnNm"] = new RequestBinding { From = "phone" },
                    },
                },
            },
        };

        string? capturedBody = null;
        var mockHttp = new MockHttpMessageHandler();
        mockHttp
            .When(HttpMethod.Post, "http://backend.test/households/lookup")
            .With(request =>
            {
                capturedBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return true;
            })
            .Respond("application/json", DcRawResponse);

        var httpClient = mockHttp.ToHttpClient();
        var backend = new ConfigurableStateBackend(configuration, httpClient);
        var request = new HouseholdLookupRequest(
            new[] { new IdentitySignal("phone", "5551234567", Verified: true) });

        // Act
        await backend.LookupHouseholdAsync(request);

        // Assert
        Assert.NotNull(capturedBody);
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
                        Fields = new Dictionary<string, string>
                        {
                            ["summerEBTCaseID"] = "sebtChldCwin",
                            ["childFirstName"] = "chldNm",
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
