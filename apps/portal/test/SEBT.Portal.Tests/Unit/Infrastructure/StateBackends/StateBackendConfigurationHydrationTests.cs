using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends.Configuration;
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
        Assert.Equal("SummerEBTCaseID", response.Fields["summerEBTCaseID"]);
        Assert.Equal("ChildFirstName", response.Fields["childFirstName"]);

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
}
