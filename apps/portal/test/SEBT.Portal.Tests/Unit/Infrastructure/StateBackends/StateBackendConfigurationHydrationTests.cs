using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends.Configuration;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;
using SEBT.Portal.Tests.Unit.Infrastructure.StateBackends.ConfigSamples;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

/// <summary>
/// Spike (DC-568): can the canonical state-backend config records hydrate from YAML via
/// YamlDotNet 18.1.0 WITHOUT modifying the Core types? Deserializer config is inline on
/// purpose — this is an experiment, not a committed Infrastructure loader.
/// </summary>
public class StateBackendConfigurationHydrationTests
{
    [Fact]
    public void Hydrates_StateBackendConfiguration_FromEmbeddedYaml()
    {
        string yaml = SampleLoader.Load("dc.sample.yaml");
        var config = StateBackendConfigurationLoader.Load(yaml);

        Assert.Equal(new Uri("http://localhost:8085"), config.BaseUrl);

        StateBackendApiKeyAuthScheme apiKeyAuth = Assert.IsType<StateBackendApiKeyAuthScheme>(config.Auth);
        Assert.Equal("X-Api-Key", apiKeyAuth.Header);
        Assert.Equal("dc-api-key", apiKeyAuth.KeyRef);

        HouseholdLookupOperationConfig? householdLookup = config.Operations.HouseholdLookup;
        Assert.NotNull(householdLookup);
        Assert.Equal(StateBackendHttpMethod.Post, householdLookup.Method);
        Assert.Equal("/households/lookup", householdLookup.Path);

        Dictionary<string, RequestBinding>? request = householdLookup.Request;
        Assert.NotNull(request);
        Assert.Equal(true, request["isIdentityProofed"].Const);
        Assert.Equal(true, request["includePendingApplicantDetails"].Const);
        Assert.Equal("email", request["guardianEmail"].From);

        Dictionary<string, RequestBinding>? guardianIdentifiers = request["guardianIdentifiers"].Compose;
        Assert.NotNull(guardianIdentifiers);
        Assert.Equal("ic", guardianIdentifiers["IC"].From);
        Assert.Equal("dob", guardianIdentifiers["DOB"].From);
        Assert.Equal("portalUuid", guardianIdentifiers["PortalUUID"].From);

        StateBackendResponseMapping? response = householdLookup.Response;
        Assert.NotNull(response);
        Assert.Equal("$.resultSets[0]", response.Root);
        Assert.Equal("SummerEBTCaseID", response.Fields["summerEBTCaseID"].From);
        Assert.Equal("ChildFirstName", response.Fields["childFirstName"].From);

        // A date-target field carries an exact parse format.
        FieldMapping issueDate = response.Fields["ebtCardIssueDate"];
        Assert.Equal("IssueDate", issueDate.From);
        Assert.Equal("MM/dd/yyyy", issueDate.Format);

        // An enum-target field references a named domain-centered table.
        FieldMapping cardStatus = response.Fields["ebtCardStatus"];
        Assert.Equal("CardStatus", cardStatus.From);
        Assert.Equal("cardStatus", cardStatus.Enum);

        // The referenced enum table hydrates domain-centered: OUR value → state token(s), plus default.
        Assert.NotNull(config.Enums);
        StateBackendEnumTable cardStatusTable = config.Enums["cardStatus"];
        Assert.Equal(new[] { "ACTIVE" }, cardStatusTable.Map["Active"]);
        Assert.Equal(new[] { "LOST", "LOST, AUTO REISSUE" }, cardStatusTable.Map["Lost"]);
        Assert.Equal("Unknown", cardStatusTable.Default);

        StateBackendDisaggregation? disaggregation = response.Disaggregation;
        Assert.NotNull(disaggregation);
        Assert.Equal(DisaggregationRule.Presence, disaggregation.Rule);
        Assert.Equal("ApplicationId", disaggregation.DiscriminatorField);
        Assert.Equal("ApplicationId", disaggregation.GroupApplicationsBy);
        Assert.Equal(CaseInclusionPredicate.All, disaggregation.CaseInclusion);

        Assert.NotNull(config.Operations.Health);

        // Capability-derivation smoke assert: nothing modeled AddressUpdate/EnrollmentCheck here.
        StateBackendCapabilities capabilities = config.Capabilities;
        Assert.False(capabilities.AddressUpdate);
        Assert.False(capabilities.EnrollmentCheck);
    }

    [Fact]
    public void Hydrates_CoStateBackendConfiguration_FromEmbeddedYaml()
    {
        string yaml = SampleLoader.Load("co.sample.yaml");
        var config = StateBackendConfigurationLoader.Load(yaml);

        Assert.Equal(new Uri("http://localhost:8086"), config.BaseUrl);

        // Exercises the OTHER auth discriminator branch (client_credentials) + the OAuth subtype.
        StateBackendOAuthClientCredentialsAuthScheme oauthAuth =
            Assert.IsType<StateBackendOAuthClientCredentialsAuthScheme>(config.Auth);
        Assert.Equal(new Uri("http://localhost:8086/oauth/token"), oauthAuth.TokenUrl);
        Assert.Equal("co-client", oauthAuth.ClientId);
        Assert.Equal("co-client-secret", oauthAuth.ClientSecretRef);

        HouseholdLookupOperationConfig? householdLookup = config.Operations.HouseholdLookup;
        Assert.NotNull(householdLookup);
        Assert.Equal(StateBackendHttpMethod.Post, householdLookup.Method);
        Assert.Equal("/sebt/get-account-details", householdLookup.Path);

        Dictionary<string, RequestBinding>? request = householdLookup.Request;
        Assert.NotNull(request);
        Assert.Equal("phone", request["PhnNm"].From);

        Assert.Equal("sebtChldCwin", householdLookup.Response?.Fields["summerEBTCaseID"].From);

        // Exercises the OTHER disaggregation branch (valueInSet) + a named caseInclusion predicate.
        StateBackendDisaggregation? disaggregation = householdLookup.Response?.Disaggregation;
        Assert.NotNull(disaggregation);
        Assert.Equal(DisaggregationRule.ValueInSet, disaggregation.Rule);
        Assert.Equal("eligSrc", disaggregation.DiscriminatorField);
        Assert.Equal(new[] { "CBMS", "PK" }, disaggregation.ApplicationValues);
        Assert.Equal(
            CaseInclusionPredicate.WhenApprovedOrNotApplicationBased,
            disaggregation.CaseInclusion);

        AddressUpdateOperationConfig? addressUpdate = config.Operations.AddressUpdate;
        Assert.NotNull(addressUpdate);
        Assert.Equal(StateBackendHttpMethod.Patch, addressUpdate.Method);

        // Capability derivation differs from the DC sample: CO models AddressUpdate.
        StateBackendCapabilities capabilities = config.Capabilities;
        Assert.True(capabilities.AddressUpdate);
        Assert.False(capabilities.EnrollmentCheck);
    }

    // A canonical value that is NOT a real member of the target C# enum must fail loud at validation.
    [Fact]
    public void ValidateEnumTables_FailsLoud_WhenCanonicalValueIsNotARealEnumMember()
    {
        StateBackendConfiguration config = BuildEnumConfig(
            new StateBackendEnumTable
            {
                Map = new Dictionary<string, List<string>>
                {
                    ["Active"] = new() { "ACTIVE" },
                    ["Frozn"] = new() { "FROZEN" }, // typo: not a CardStatus member
                },
                Default = "Unknown",
            });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendResponseMapper.ValidateEnumTables(config));
        Assert.Contains("Frozn", ex.Message);
    }

    // A single source token mapped under two of OUR values is ambiguous and must fail loud.
    [Fact]
    public void ValidateEnumTables_FailsLoud_WhenTokenIsAmbiguous()
    {
        StateBackendConfiguration config = BuildEnumConfig(
            new StateBackendEnumTable
            {
                Map = new Dictionary<string, List<string>>
                {
                    ["Active"] = new() { "ISSUED" },
                    ["Processed"] = new() { "ISSUED" }, // same token under two canonical values
                },
                Default = "Unknown",
            });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendResponseMapper.ValidateEnumTables(config));
        Assert.Contains("ISSUED", ex.Message);
    }

    // Minimal config whose household lookup maps ebtCardStatus through a named "cardStatus" table.
    private static StateBackendConfiguration BuildEnumConfig(StateBackendEnumTable cardStatusTable) =>
        new()
        {
            BaseUrl = new Uri("http://backend.test"),
            Auth = new StateBackendApiKeyAuthScheme { Header = "X-Api-Key", KeyRef = "k" },
            Operations = new StateBackendOperations
            {
                HouseholdLookup = new HouseholdLookupOperationConfig
                {
                    Method = StateBackendHttpMethod.Post,
                    Path = "/lookup",
                    Response = new StateBackendResponseMapping
                    {
                        Root = "$.records",
                        Fields = new Dictionary<string, FieldMapping>
                        {
                            ["ebtCardStatus"] = new() { From = "CardStatus", Enum = "cardStatus" },
                        },
                    },
                },
            },
            Enums = new Dictionary<string, StateBackendEnumTable>
            {
                ["cardStatus"] = cardStatusTable,
            },
        };
}
