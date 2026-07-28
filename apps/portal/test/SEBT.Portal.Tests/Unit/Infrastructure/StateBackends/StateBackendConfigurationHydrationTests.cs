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

        RequestBinding? request = householdLookup.Request;
        Assert.NotNull(request);

        // includePendingApplicantDetails is genuinely fixed policy — a constant, value false.
        Assert.NotNull(request.Constants);
        Assert.Equal(false, request.Constants["includePendingApplicantDetails"]);

        // isIdentityProofed is per-request (caller proofing) — a map pass-through, NOT a constant.
        Assert.DoesNotContain("isIdentityProofed", request.Constants.Keys);

        Assert.NotNull(request.Map);
        Assert.Equal("guardianEmail", request.Map["email"]);
        Assert.Equal("guardianIdentifiers.IC", request.Map["ic"]);
        Assert.Equal("guardianIdentifiers.DOB", request.Map["dob"]);
        Assert.Equal("guardianIdentifiers.PortalUUID", request.Map["portalUuid"]);
        Assert.Equal("isIdentityProofed", request.Map["isProofed"]);

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

        // The keywordRules brick hydrates: multi-source `from`, ordered `order`, domain-centered
        // `map` (OUR value -> substrings that indicate it), and a `default`.
        FieldMapping issuanceType = response.Fields["issuanceType"];
        Assert.Equal(new[] { "HouseholdType", "EligibilityType" }, issuanceType.From.All);
        KeywordRules keywordRules = Assert.IsType<KeywordRules>(issuanceType.KeywordRules);
        Assert.Equal(new[] { "SummerEbt", "SnapEbtCard", "TanfEbtCard" }, keywordRules.Order);
        Assert.Equal(new[] { "OSSE", "NSLP" }, keywordRules.Map["SummerEbt"]);
        Assert.Equal(new[] { "FOOD", "SNAP" }, keywordRules.Map["SnapEbtCard"]);
        Assert.Equal(new[] { "CASH", "TANF" }, keywordRules.Map["TanfEbtCard"]);
        Assert.Equal("Unknown", keywordRules.Default);

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

        // The opaque caseId composition hydrates: OUR routing-field name → source property.
        CaseIdComposition caseId = Assert.IsType<CaseIdComposition>(response.CaseId);
        Assert.Equal("SummerEBTCaseID", caseId.Fields["caseId"]);
        Assert.Equal("ApplicationId", caseId.Fields["applicationId"]);

        // The card-replacement write op hydrates: request binding + ordered result classifier.
        CardReplacementOperationConfig? cardReplacement = config.Operations.CardReplacement;
        Assert.NotNull(cardReplacement);
        Assert.Equal(StateBackendHttpMethod.Post, cardReplacement.Method);
        Assert.Equal("/cards/replace", cardReplacement.Path);

        Assert.NotNull(cardReplacement.Request);
        Assert.Equal("portal", cardReplacement.Request.Constants!["source"]);
        Assert.Equal("summerEbtCaseId", cardReplacement.Request.Map!["caseId"]);
        Assert.Equal("applicationId", cardReplacement.Request.Map["applicationId"]);
        Assert.Equal("reason", cardReplacement.Request.Map["reason"]);

        ResultClassifier classifier = Assert.IsType<ResultClassifier>(cardReplacement.Result);
        Assert.Equal(2, classifier.Conditions.Count);
        Assert.Equal(CardReplacementOutcome.PolicyRejection, classifier.Conditions[0].Outcome);
        Assert.Equal("message", classifier.Conditions[0].MessageField);
        Assert.Equal(new[] { "policy" }, classifier.Conditions[0].MessageContains);
        Assert.Equal(CardReplacementOutcome.Success, classifier.Conditions[1].Outcome);
        Assert.Equal("resultCode", classifier.Conditions[1].Field);
        Assert.Equal(new[] { "OK" }, classifier.Conditions[1].ValueIn);
        Assert.Equal(CardReplacementOutcome.BackendError, classifier.Default);

        // The DC address-update write op hydrates: constants + the SHARED batch shape + scalar map
        // (address fields) + the reused result classifier.
        AddressUpdateOperationConfig? dcAddressUpdate = config.Operations.AddressUpdate;
        Assert.NotNull(dcAddressUpdate);
        Assert.Equal(StateBackendHttpMethod.Post, dcAddressUpdate.Method);
        Assert.Equal("/households/address", dcAddressUpdate.Path);

        Assert.NotNull(dcAddressUpdate.Request);
        Assert.Equal("portal", dcAddressUpdate.Request.Constants!["source"]);
        Assert.Equal("householdIdentifier", dcAddressUpdate.Request.Shared!["householdEmail"]);
        Assert.Null(dcAddressUpdate.Request.Collect);
        Assert.Equal("address.line1", dcAddressUpdate.Request.Map!["line1"]);
        Assert.Equal("address.city", dcAddressUpdate.Request.Map["city"]);
        Assert.Equal("address.state", dcAddressUpdate.Request.Map["state"]);
        Assert.Equal("address.zip", dcAddressUpdate.Request.Map["zip"]);

        ResultClassifier dcAddressClassifier = Assert.IsType<ResultClassifier>(dcAddressUpdate.Result);
        ResultCondition dcAddressSuccess = Assert.Single(dcAddressClassifier.Conditions);
        Assert.Equal(CardReplacementOutcome.Success, dcAddressSuccess.Outcome);
        Assert.Equal("resultCode", dcAddressSuccess.Field);
        Assert.Equal(new[] { "OK" }, dcAddressSuccess.ValueIn);
        Assert.Equal(CardReplacementOutcome.BackendError, dcAddressClassifier.Default);

        // The DC enrollment op hydrates: PerChild fan-out (no index, no expansion) + single-object
        // match on the boolean eligibility flag.
        EnrollmentCheckOperationConfig? dcEnrollment = config.Operations.EnrollmentCheck;
        Assert.NotNull(dcEnrollment);
        Assert.Equal(StateBackendHttpMethod.Post, dcEnrollment.Method);
        Assert.Equal("/enrollment/check", dcEnrollment.Path);
        Assert.Equal(EnrollmentCallMode.PerChild, dcEnrollment.CallMode);

        Assert.NotNull(dcEnrollment.Request);
        Assert.Equal(CandidateExpansion.None, dcEnrollment.Request.Expand);
        Assert.Null(dcEnrollment.Request.IndexField);
        Assert.Equal("firstName", dcEnrollment.Request.Map["firstName"]);
        Assert.Equal("dateOfBirth", dcEnrollment.Request.Map["dob"]);

        Assert.NotNull(dcEnrollment.Response);
        Assert.Equal("$", dcEnrollment.Response.Root);
        Assert.Null(dcEnrollment.Response.IndexField);
        Assert.Equal("isEligible", dcEnrollment.Response.MatchWhen.Field);
        Assert.Equal(new[] { "true" }, dcEnrollment.Response.MatchWhen.ValueIn);

        Assert.NotNull(config.Operations.Health);

        // Capability-derivation smoke assert: the modeled cardReplacement op derives a per-case
        // capability, addressUpdate derives its capability, and the modeled enrollment op derives
        // EnrollmentCheck.
        StateBackendCapabilities capabilities = config.Capabilities;
        Assert.Equal(CardReplacementCapability.PerCase, capabilities.CardReplacement);
        Assert.True(capabilities.AddressUpdate);
        Assert.True(capabilities.EnrollmentCheck);
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

        RequestBinding? request = householdLookup.Request;
        Assert.NotNull(request);
        Assert.NotNull(request.Map);
        Assert.Equal("PhnNm", request.Map["phone"]);

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

        // The status field the WhenApprovedOrNotApplicationBased predicate reads: CO's sebtAppSts,
        // resolved to canonical ApplicationStatus through a named domain-centered enum table.
        FieldMapping applicationStatus = householdLookup.Response!.Fields["applicationStatus"];
        Assert.Equal("sebtAppSts", applicationStatus.From);
        Assert.Equal("applicationStatus", applicationStatus.Enum);

        Assert.NotNull(config.Enums);
        StateBackendEnumTable applicationStatusTable = config.Enums["applicationStatus"];
        Assert.Equal(new[] { "AP" }, applicationStatusTable.Map["Approved"]);
        Assert.Equal(new[] { "DE" }, applicationStatusTable.Map["Denied"]);
        Assert.Equal("Unknown", applicationStatusTable.Default);

        AddressUpdateOperationConfig? addressUpdate = config.Operations.AddressUpdate;
        Assert.NotNull(addressUpdate);
        Assert.Equal(StateBackendHttpMethod.Patch, addressUpdate.Method);
        Assert.Equal("/sebt/update-std-dtls", addressUpdate.Path);

        // CO uses the OTHER batch shape: COLLECT per-case write-ids into an array (no shared field).
        Assert.NotNull(addressUpdate.Request);
        Assert.Equal("cases", addressUpdate.Request.Collect!["writeId"]);
        Assert.Null(addressUpdate.Request.Shared);
        Assert.Equal("stdAddr", addressUpdate.Request.Map!["line1"]);
        Assert.Equal("stdZip", addressUpdate.Request.Map["zip"]);

        ResultClassifier coAddressClassifier = Assert.IsType<ResultClassifier>(addressUpdate.Result);
        ResultCondition coAddressSuccess = Assert.Single(coAddressClassifier.Conditions);
        Assert.Equal(CardReplacementOutcome.Success, coAddressSuccess.Outcome);
        Assert.Equal("respCd", coAddressSuccess.Field);
        Assert.Equal(new[] { "200", "00" }, coAddressSuccess.ValueIn);

        // The CO enrollment op hydrates: the transposeMonthDay candidate-expansion brick + fan-in.
        EnrollmentCheckOperationConfig? coEnrollment = config.Operations.EnrollmentCheck;
        Assert.NotNull(coEnrollment);
        Assert.Equal(StateBackendHttpMethod.Post, coEnrollment.Method);
        Assert.Equal("/sebt/check-enrollment", coEnrollment.Path);

        Assert.Equal(EnrollmentCallMode.Batch, coEnrollment.CallMode);
        Assert.NotNull(coEnrollment.Request);
        Assert.Equal(CandidateExpansion.TransposeMonthDay, coEnrollment.Request.Expand);
        Assert.Equal("stdReqInd", coEnrollment.Request.IndexField);
        Assert.Equal("stdFirstName", coEnrollment.Request.Map["firstName"]);
        Assert.Equal("stdLastName", coEnrollment.Request.Map["lastName"]);
        Assert.Equal("stdDob", coEnrollment.Request.Map["dob"]);

        Assert.NotNull(coEnrollment.Response);
        Assert.Equal("$.stdntDtls", coEnrollment.Response.Root);
        Assert.Equal("stdReqInd", coEnrollment.Response.IndexField);
        Assert.Equal("sebtEligSts", coEnrollment.Response.MatchWhen.Field);
        Assert.Equal(new[] { "Y" }, coEnrollment.Response.MatchWhen.ValueIn);

        // Capability derivation: CO models AddressUpdate and EnrollmentCheck.
        StateBackendCapabilities capabilities = config.Capabilities;
        Assert.True(capabilities.AddressUpdate);
        Assert.True(capabilities.EnrollmentCheck);
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

    // A keywordRules order/map value that is NOT a real IssuanceType member must fail loud at
    // validation — the same fail-loud discipline as the enum tables.
    [Fact]
    public void ValidateEnumTables_FailsLoud_WhenKeywordRuleValueIsNotARealIssuanceType()
    {
        StateBackendConfiguration config = BuildIssuanceKeywordConfig(
            new KeywordRules
            {
                Order = new List<string> { "SummerEbt", "Snap" }, // "Snap" is not an IssuanceType member
                Map = new Dictionary<string, List<string>>
                {
                    ["SummerEbt"] = new() { "OSSE" },
                    ["Snap"] = new() { "SNAP" },
                },
                Default = "Unknown",
            });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendResponseMapper.ValidateEnumTables(config));
        Assert.Contains("Snap", ex.Message);
    }

    // Minimal config whose household lookup infers issuanceType via a keywordRules brick.
    private static StateBackendConfiguration BuildIssuanceKeywordConfig(KeywordRules keywordRules) =>
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
                            ["issuanceType"] = new()
                            {
                                From = new[] { "HouseholdType", "EligibilityType" },
                                KeywordRules = keywordRules,
                            },
                        },
                    },
                },
            },
        };

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
