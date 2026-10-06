using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Auth;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends.Configuration;
using SEBT.Portal.Tests.Unit.Infrastructure.StateBackends.ConfigSamples;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

/// <summary>
/// Hydrates the canonical state-backend config records from YAML and asserts the shape of both
/// sample bundles, plus the validator's fail-loud checks.
/// </summary>
public class StateBackendConfigurationHydrationTests
{
    [Fact]
    public void Hydrates_ApiKeySample_FromEmbeddedYaml()
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

        Assert.NotNull(request.Constants);
        Assert.Equal(false, request.Constants["includePendingApplicantDetails"]);

        // isIdentityProofed must be a per-request map pass-through, never a constant — hardcoding it
        // would bypass the lookup's proofing gate.
        Assert.DoesNotContain("isIdentityProofed", request.Constants.Keys);

        Assert.NotNull(request.Map);
        Assert.Equal("guardianEmail", request.Map["email"]);
        Assert.Equal("guardianIdentifiers.IC", request.Map["ic"]);
        Assert.Equal("guardianIdentifiers.DOB", request.Map["dob"]);
        Assert.Equal("guardianIdentifiers.PortalUUID", request.Map["portalUuid"]);
        Assert.Equal("isIdentityProofed", request.Map["isProofed"]);

        // Optional inputs bind if present and are omitted from the request body when absent.
        Assert.NotNull(request.MapOptional);
        Assert.Equal("guardianIdentifiers.SocureUUID", request.MapOptional["socureUuid"]);

        StateBackendResponseMapping? response = householdLookup.Response;
        Assert.NotNull(response);
        Assert.Equal("$.resultSets[0]", response.Root);
        Assert.Equal("SummerEBTCaseID", response.Fields["summerEBTCaseID"].From);
        Assert.Equal("ChildFirstName", response.Fields["childFirstName"].From);

        // Date fields keep their source names and use an ISO 8601 format.
        FieldMapping issueDate = response.Fields["ebtCardIssueDate"];
        Assert.Equal("EbtCardIssueDate", issueDate.From);
        Assert.Equal("yyyy-MM-ddTHH:mm:ss", issueDate.Format);

        FieldMapping expirationDate = response.Fields["benefitExpirationDate"];
        Assert.Equal("BenefitExpirationDate", expirationDate.From);
        Assert.Equal("yyyy-MM-ddTHH:mm:ss", expirationDate.Format);

        FieldMapping cardStatus = response.Fields["ebtCardStatus"];
        Assert.Equal("EbtCardStatus", cardStatus.From);
        Assert.Equal("cardStatus", cardStatus.Enum);

        FieldMapping issuanceType = response.Fields["issuanceType"];
        Assert.Equal(new[] { "HouseholdType", "EligibilityType" }, issuanceType.From!.All);
        KeywordRules keywordRules = Assert.IsType<KeywordRules>(issuanceType.KeywordRules);
        Assert.Equal(new[] { "SummerEbt", "SnapEbtCard", "TanfEbtCard" }, keywordRules.Order);
        Assert.Equal(new[] { "OSSE", "NSLP" }, keywordRules.Map["SummerEbt"]);
        Assert.Equal(new[] { "FOOD", "SNAP" }, keywordRules.Map["SnapEbtCard"]);
        Assert.Equal(new[] { "CASH", "TANF" }, keywordRules.Map["TanfEbtCard"]);
        Assert.Equal("Unknown", keywordRules.Default);

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

        CaseIdComposition caseId = Assert.IsType<CaseIdComposition>(response.CaseId);
        Assert.Equal("SummerEBTCaseID", caseId.Fields["caseId"]);
        Assert.Equal("ApplicationId", caseId.Fields["applicationId"]);

        CardReplacementOperationConfig? cardReplacement = config.Operations.CardReplacement;
        Assert.NotNull(cardReplacement);
        Assert.Equal(StateBackendHttpMethod.Post, cardReplacement.Method);
        Assert.Equal("/card-replacements", cardReplacement.Path);

        // The wrapper's request model is the sproc's three inputs; the canonical contract carries
        // no replacement reason, so only the two token-carried routing fields bind.
        Assert.NotNull(cardReplacement.Request);
        Assert.Null(cardReplacement.Request.Constants);
        Assert.Equal("summerEbtCaseId", cardReplacement.Request.Map!["caseId"]);
        Assert.Equal("householdEmail", cardReplacement.Request.Map["householdIdentifier"]);

        // The wrapper returns the sproc's raw OUTPUT params: numeric resultCode + resultMessage.
        ResultClassifier classifier = Assert.IsType<ResultClassifier>(cardReplacement.Result);
        Assert.Equal(2, classifier.Conditions.Count);
        Assert.Equal(WriteOutcome.PolicyRejection, classifier.Conditions[0].Outcome);
        Assert.Equal("resultMessage", classifier.Conditions[0].MessageField);
        Assert.Equal(new[] { "policy" }, classifier.Conditions[0].MessageContains);
        Assert.Equal(WriteOutcome.Success, classifier.Conditions[1].Outcome);
        Assert.Equal("resultCode", classifier.Conditions[1].Field);
        Assert.Equal(new[] { "0" }, classifier.Conditions[1].ValueIn);
        Assert.Equal(WriteOutcome.BackendError, classifier.Default);

        // Address update posts one object. Household identifier and address scalars come from the map.
        AddressUpdateOperationConfig? addressUpdate = config.Operations.AddressUpdate;
        Assert.NotNull(addressUpdate);
        Assert.Equal(StateBackendHttpMethod.Post, addressUpdate.Method);
        Assert.Equal("/households/address", addressUpdate.Path);

        Assert.NotNull(addressUpdate.Request);
        Assert.Equal("portal", addressUpdate.Request.Constants!["source"]);
        Assert.Null(addressUpdate.Request.Shared);
        Assert.Null(addressUpdate.Request.Collect);
        Assert.Equal("householdIdentifier", addressUpdate.Request.Map!["householdIdentifier"]);
        Assert.Equal("address.line1", addressUpdate.Request.Map["line1"]);
        Assert.Equal("address.city", addressUpdate.Request.Map["city"]);
        Assert.Equal("address.state", addressUpdate.Request.Map["state"]);
        Assert.Equal("address.zip", addressUpdate.Request.Map["zip"]);
        Assert.NotNull(addressUpdate.Request.MapOptional);
        Assert.Equal("address.line2", addressUpdate.Request.MapOptional["line2"]);

        ResultClassifier addressClassifier = Assert.IsType<ResultClassifier>(addressUpdate.Result);
        ResultCondition addressSuccess = Assert.Single(addressClassifier.Conditions);
        Assert.Equal(WriteOutcome.Success, addressSuccess.Outcome);
        Assert.Equal("resultCode", addressSuccess.Field);
        Assert.Equal(new[] { "OK" }, addressSuccess.ValueIn);
        Assert.Equal(WriteOutcome.BackendError, addressClassifier.Default);

        // Enrollment fans out per child: no index field and no candidate expansion.
        EnrollmentCheckOperationConfig? enrollment = config.Operations.EnrollmentCheck;
        Assert.NotNull(enrollment);
        Assert.Equal(StateBackendHttpMethod.Post, enrollment.Method);
        Assert.Equal("/enrollment/check", enrollment.Path);
        Assert.Equal(EnrollmentCallMode.PerChild, enrollment.CallMode);

        Assert.NotNull(enrollment.Request);
        Assert.Equal(CandidateExpansion.None, enrollment.Request.Expand);
        Assert.Null(enrollment.Request.IndexField);
        Assert.Equal("firstName", enrollment.Request.Map["firstName"]);
        Assert.Equal("dateOfBirth", enrollment.Request.Map["dob"]);

        // schoolIdentifier is optional: it binds when present and is omitted otherwise.
        Assert.NotNull(enrollment.Request.MapOptional);
        Assert.Equal("schoolName", enrollment.Request.MapOptional["schoolIdentifier"]);

        Assert.NotNull(enrollment.Response);
        Assert.Equal("$", enrollment.Response.Root);
        Assert.Null(enrollment.Response.IndexField);
        Assert.Equal(EnrollmentMatchStrategy.AnyRowValueIn, enrollment.Response.Match.Strategy);
        Assert.Equal("isEligible", enrollment.Response.Match.Field);
        Assert.Equal(new[] { "true" }, enrollment.Response.Match.ValueIn);

        // This response declares no per-row or result-level message carriers.
        Assert.Null(enrollment.Response.StatusMessageField);
        Assert.Null(enrollment.Response.MessageField);

        Assert.NotNull(config.Operations.Health);

        // Capabilities derive from which operations the config declares.
        StateBackendCapabilities capabilities = config.Capabilities;
        Assert.Equal(CardReplacementCapability.PerCase, capabilities.CardReplacement);
        Assert.True(capabilities.AddressUpdate);
        Assert.True(capabilities.EnrollmentCheck);
    }

    [Fact]
    public void Hydrates_ClientCredentialsSample_FromEmbeddedYaml()
    {
        string yaml = SampleLoader.Load("co.sample.yaml");
        var config = StateBackendConfigurationLoader.Load(yaml);

        Assert.Equal(new Uri("http://localhost:8086"), config.BaseUrl);

        // This sample uses client_credentials. The api-key sample covers the other auth branch.
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
        Assert.Equal("phnNm", request.Map["phone"]);

        Assert.Equal("sebtChldCwin", householdLookup.Response?.Fields["summerEBTCaseID"].From);

        // This sample disaggregates with valueInSet. The api-key sample uses presence.
        StateBackendDisaggregation? disaggregation = householdLookup.Response?.Disaggregation;
        Assert.NotNull(disaggregation);
        Assert.Equal(DisaggregationRule.ValueInSet, disaggregation.Rule);
        Assert.Equal("eligSrc", disaggregation.DiscriminatorField);
        Assert.Equal(new[] { "CBMS", "PK" }, disaggregation.ApplicationValues);
        Assert.Equal(
            CaseInclusionPredicate.WhenApprovedOrNotApplicationBased,
            disaggregation.CaseInclusion);

        // The status the WhenApprovedOrNotApplicationBased predicate reads.
        FieldMapping applicationStatus = householdLookup.Response!.Fields["applicationStatus"];
        Assert.Equal("stdntEligSts", applicationStatus.From);
        Assert.Equal("applicationStatus", applicationStatus.Enum);
        Assert.Equal("stdLstNm", householdLookup.Response.Fields["childLastName"].From);
        Assert.Equal("SummerEbt", householdLookup.Response.Fields["issuanceType"].Value);
        Assert.Equal("addrLn1", householdLookup.Response.MailingAddress!.Line1);
        Assert.Equal("zip4", householdLookup.Response.MailingAddress.Zip4);

        FieldMapping expirationDate = householdLookup.Response.Fields["benefitExpirationDate"];
        Assert.Equal("benExpDt", expirationDate.From);
        Assert.Equal("yyyy-MM-dd", expirationDate.Format);

        Assert.NotNull(config.Enums);
        StateBackendEnumTable applicationStatusTable = config.Enums["applicationStatus"];
        Assert.Equal(new[] { "AP" }, applicationStatusTable.Map["Approved"]);
        Assert.Equal(new[] { "DE", "OT" }, applicationStatusTable.Map["Denied"]);
        Assert.Equal(new[] { "AI", "AM", "PD", "PE", "PG", "PS" }, applicationStatusTable.Map["Pending"]);
        Assert.Equal("Unknown", applicationStatusTable.Default);
        Assert.Equal(new[] { "ACTIVE" }, config.Enums["cardStatus"].Map["Active"]);

        AddressUpdateOperationConfig? addressUpdate = config.Operations.AddressUpdate;
        Assert.NotNull(addressUpdate);
        Assert.Equal(StateBackendHttpMethod.Patch, addressUpdate.Method);
        Assert.Equal("/sebt/update-std-dtls", addressUpdate.Path);

        // Address update PATCHes one object per case. Address scalars bind under a nested addr object.
        Assert.NotNull(addressUpdate.Request);
        Assert.True(addressUpdate.Request.EachCase);
        Assert.Null(addressUpdate.Request.Collect);
        Assert.Null(addressUpdate.Request.Shared);
        Assert.Equal("sebtChldId", addressUpdate.Request.Map!["sebtChldId"]);
        Assert.Equal("sebtAppId", addressUpdate.Request.Map["sebtAppId"]);
        Assert.Equal("addr.addrLn1", addressUpdate.Request.Map["line1"]);
        Assert.Equal("addr.cty", addressUpdate.Request.Map["city"]);
        Assert.Equal("addr.staCd", addressUpdate.Request.Map["state"]);
        Assert.Equal("addr.zip", addressUpdate.Request.Map["zip"]);
        Assert.Equal("addr.addrLn2", addressUpdate.Request.MapOptional!["line2"]);
        Assert.Equal("addr.zip4", addressUpdate.Request.MapOptional["zip4"]);

        CaseIdComposition caseId = Assert.IsType<CaseIdComposition>(householdLookup.Response.CaseId);
        Assert.Equal("sebtChldId", caseId.Fields["sebtChldId"]);
        Assert.Equal("sebtAppId", caseId.Fields["sebtAppId"]);

        ResultClassifier addressClassifier = Assert.IsType<ResultClassifier>(addressUpdate.Result);
        ResultCondition addressSuccess = Assert.Single(addressClassifier.Conditions);
        Assert.Equal(WriteOutcome.Success, addressSuccess.Outcome);
        Assert.Equal("respCd", addressSuccess.Field);
        Assert.Equal(new[] { "200", "00" }, addressSuccess.ValueIn);

        CardReplacementOperationConfig? cardReplacement = config.Operations.CardReplacement;
        Assert.NotNull(cardReplacement);
        Assert.Equal(StateBackendHttpMethod.Patch, cardReplacement.Method);
        Assert.Equal("/sebt/update-std-dtls", cardReplacement.Path);
        Assert.Equal(CardReplacementCallMode.Batch, cardReplacement.CallMode);
        Assert.True(cardReplacement.Request!.EachCase);
        Assert.Null(cardReplacement.Request.Collect);
        Assert.Equal("sebtChldId", cardReplacement.Request.Map!["sebtChldId"]);
        Assert.Equal("sebtAppId", cardReplacement.Request.Map["sebtAppId"]);
        Assert.Equal("Y", cardReplacement.Request.Constants!["reqNewCard"]);

        // Enrollment is one batch call: transposeMonthDay expansion and a confidenceThreshold match.
        EnrollmentCheckOperationConfig? enrollment = config.Operations.EnrollmentCheck;
        Assert.NotNull(enrollment);
        Assert.Equal(StateBackendHttpMethod.Post, enrollment.Method);
        Assert.Equal("/sebt/check-enrollment", enrollment.Path);

        Assert.Equal(EnrollmentCallMode.Batch, enrollment.CallMode);
        Assert.NotNull(enrollment.Request);
        Assert.Equal(CandidateExpansion.TransposeMonthDay, enrollment.Request.Expand);
        Assert.Equal("stdReqInd", enrollment.Request.IndexField);
        Assert.Equal("stdFirstName", enrollment.Request.Map["firstName"]);
        Assert.Equal("stdLastName", enrollment.Request.Map["lastName"]);
        Assert.Equal("stdDob", enrollment.Request.Map["dob"]);

        // schoolIdentifier is optional: it binds when present and is omitted otherwise.
        Assert.NotNull(enrollment.Request.MapOptional);
        Assert.Equal("stdSchlCd", enrollment.Request.MapOptional["schoolIdentifier"]);

        Assert.NotNull(enrollment.Response);
        Assert.Equal("$.stdntDtls", enrollment.Response.Root);
        Assert.Equal("stdReqInd", enrollment.Response.IndexField);

        // The winning row's eligibility text is per child. The root respMsg is the result-level message.
        Assert.Equal("sebtEligSts", enrollment.Response.StatusMessageField);
        Assert.Equal("respMsg", enrollment.Response.MessageField);
        Assert.Equal(EnrollmentMatchStrategy.ConfidenceThreshold, enrollment.Response.Match.Strategy);
        Assert.Equal("mtchCnfd", enrollment.Response.Match.ScoreField);
        Assert.Equal(90.0, enrollment.Response.Match.Threshold);

        // A match also requires the best row's eligibility flag, not the score alone.
        Assert.Equal("sebtEligSts", enrollment.Response.Match.Field);
        Assert.Equal(new[] { "Y" }, enrollment.Response.Match.ValueIn);

        StateBackendCapabilities capabilities = config.Capabilities;
        Assert.Equal(CardReplacementCapability.Batch, capabilities.CardReplacement);
        Assert.True(capabilities.AddressUpdate);
        Assert.True(capabilities.EnrollmentCheck);
    }

    // A canonical value that is NOT a real member of the target C# enum must fail loud at LOAD time.
    [Fact]
    public void Validate_FailsLoud_WhenCanonicalValueIsNotARealEnumMember()
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
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("Frozn", ex.Message);
    }

    // A single source token mapped under two of OUR values is ambiguous and must fail loud.
    [Fact]
    public void Validate_FailsLoud_WhenTokenIsAmbiguous()
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
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("ISSUED", ex.Message);
    }

    [Fact]
    public void Validate_FailsLoud_WhenTokenIsAmbiguous_IgnoringCase()
    {
        StateBackendConfiguration config = BuildEnumConfig(
            new StateBackendEnumTable
            {
                Map = new Dictionary<string, List<string>>
                {
                    ["Active"] = new() { "ACTIVE" },
                    ["Processed"] = new() { "Active" },
                },
                Default = "Unknown",
            });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("Active", ex.Message);
    }

    // A keywordRules value that is NOT a real IssuanceType member must fail loud at load.
    [Fact]
    public void Validate_FailsLoud_WhenKeywordRuleValueIsNotARealIssuanceType()
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
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("Snap", ex.Message);
    }

    // A malformed classifier (a condition setting no closed kind) fails loud at load — the
    // validator checks BOTH write ops.
    [Theory]
    [InlineData(true)] // card replacement
    [InlineData(false)] // address update
    public void Validate_FailsLoud_WhenWriteClassifierConditionSetsNoKind(bool onCardReplacement)
    {
        StateBackendConfiguration config = BuildWriteClassifierConfig(
            cardReplacementClassifier: onCardReplacement ? MalformedClassifier() : null,
            addressUpdateClassifier: onCardReplacement ? null : MalformedClassifier());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("exactly one", ex.Message);
    }

    // mapOptional on a write is bind-if-present. Address line2 is optional, so setting it on
    // either write op must load rather than fail.
    [Theory]
    [InlineData(true)] // card replacement
    [InlineData(false)] // address update
    public void Validate_AllowsMapOptional_OnWriteOperations(bool onCardReplacement)
    {
        StateBackendConfiguration config = BuildWriteMapOptionalConfig(
            cardReplacementRequest: onCardReplacement ? MapOptionalRequestBinding() : null,
            addressUpdateRequest: onCardReplacement ? null : MapOptionalRequestBinding());

        StateBackendConfigurationValidator.Validate(config);
    }

    [Fact]
    public void Load_FailsLoud_WhenCaseIdDeclaresFromContext()
    {
        const string yaml = """
            baseUrl: http://backend.test
            auth:
              scheme: api_key
              header: X-Api-Key
              keyRef: test-api-key
            operations:
              householdLookup:
                method: post
                path: /lookup
                request:
                  map:
                    email: guardianEmail
                response:
                  root: $.records
                  fields:
                    childFirstName:
                      from: ChildFirstName
                  caseId:
                    fields:
                      caseId: SummerEBTCaseID
                    fromContext:
                      householdEmail: householdIdentifier
            """;

        Assert.ThrowsAny<Exception>(() => StateBackendConfigurationLoader.Load(yaml));
    }

    // valueInSet without a list would silently treat every row as not application-based.
    [Fact]
    public void Validate_FailsLoud_WhenValueInSetHasNoApplicationValues()
    {
        StateBackendConfiguration config = StateBackendTestConfig.Base().WithLookup(
            new HouseholdLookupOperationConfig
            {
                Method = StateBackendHttpMethod.Post,
                Path = "/lookup",
                Response = new StateBackendResponseMapping
                {
                    Root = "$.records",
                    Fields = new Dictionary<string, FieldMapping>
                    {
                        ["childFirstName"] = new() { From = "ChildFirstName" },
                    },
                    Disaggregation = new StateBackendDisaggregation
                    {
                        Rule = DisaggregationRule.ValueInSet,
                        DiscriminatorField = "eligSrc",
                        CaseInclusion = CaseInclusionPredicate.All,
                    },
                },
            });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("applicationValues", ex.Message);
    }

    // An empty keyword would match every haystack (Contains("")) — reject at load.
    [Fact]
    public void Validate_FailsLoud_WhenKeywordRulesMapContainsEmptyKeyword()
    {
        StateBackendConfiguration config = BuildIssuanceKeywordConfig(
            new KeywordRules
            {
                Order = new List<string> { "SummerEbt" },
                Map = new Dictionary<string, List<string>>
                {
                    ["SummerEbt"] = new() { "OSSE", "  " },
                },
                Default = "Unknown",
            });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("empty keyword", ex.Message);
    }

    // A path-only write op would advertise the feature and fail on the first real attempt.
    [Fact]
    public void Validate_FailsLoud_WhenWriteOperationIsIncomplete()
    {
        StateBackendConfiguration config = StateBackendTestConfig.Base() with
        {
            Operations = new StateBackendOperations
            {
                CardReplacement = new CardReplacementOperationConfig
                {
                    Method = StateBackendHttpMethod.Patch,
                    Path = "/sebt/update-std-dtls",
                },
            },
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("incomplete", ex.Message);
        Assert.Contains("cardReplacement", ex.Message);
    }

    [Fact]
    public void Load_FailsLoud_OnUnmatchedYamlProperty()
    {
        const string yaml = """
            baseUrl: http://backend.test
            auth:
              scheme: api_key
              header: X-Api-Key
              keyRef: test-api-key
            operations: {}
            notARealProperty: true
            """;

        Assert.ThrowsAny<Exception>(() => StateBackendConfigurationLoader.Load(yaml));
    }

    // A date-typed target without an exact 'format' must fail at LOAD, not on the first mapped
    // record.
    [Fact]
    public void Validate_FailsLoud_WhenDateFieldHasNoFormat()
    {
        StateBackendConfiguration config = BuildLookupFieldsConfig(
            new Dictionary<string, FieldMapping>
            {
                ["ebtCardIssueDate"] = new() { From = "IssueDate" }, // no format
            });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("ebtCardIssueDate", ex.Message);
        Assert.Contains("format", ex.Message);
    }

    // A fields entry naming a target outside the closed canonical set must fail at LOAD, not on
    // the first mapped record.
    [Fact]
    public void Validate_FailsLoud_WhenFieldNamesUnknownCanonicalTarget()
    {
        StateBackendConfiguration config = BuildLookupFieldsConfig(
            new Dictionary<string, FieldMapping>
            {
                ["ebtCardIssueDte"] = new() { From = "IssueDate" }, // typo: not a canonical target
            });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("ebtCardIssueDte", ex.Message);
    }

    [Fact]
    public void Validate_FailsLoud_WhenFromSequenceHasNoKeywordRules()
    {
        StateBackendConfiguration config = BuildLookupFieldsConfig(
            new Dictionary<string, FieldMapping>
            {
                ["childFirstName"] = new()
                {
                    From = new[] { "HouseholdType", "EligibilityType" },
                },
            });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("childFirstName", ex.Message);
        Assert.Contains("keywordRules", ex.Message);
    }

    [Fact]
    public void Validate_FailsLoud_WhenResponseRootPathIsMalformed()
    {
        StateBackendConfiguration config = BuildLookupFieldsConfig(
            new Dictionary<string, FieldMapping>
            {
                ["childFirstName"] = new() { From = "ChildFirstName" },
            });
        config = config.WithLookupResponse(
            config.Operations.HouseholdLookup!.Response! with { Root = "$.resultSets[-1]" });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("$.resultSets[-1]", ex.Message);
    }

    [Fact]
    public void Validate_FailsLoud_WhenRequestMapPathHasEmptySegment()
    {
        StateBackendConfiguration config = BuildWriteMapOptionalConfig(
            cardReplacementRequest: new RequestBinding
            {
                Map = new Dictionary<string, string> { ["caseId"] = "summer..EbtCaseId" },
            },
            addressUpdateRequest: null);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("summer..EbtCaseId", ex.Message);
    }

    // A messageContains condition without a messageField has no body property to read.
    [Fact]
    public void Validate_FailsLoud_WhenMessageContainsHasNoMessageField()
    {
        StateBackendConfiguration config = BuildWriteClassifierConfig(
            cardReplacementClassifier: new ResultClassifier
            {
                Conditions = new List<ResultCondition>
                {
                    new()
                    {
                        Outcome = WriteOutcome.PolicyRejection,
                        MessageContains = new List<string> { "policy" },
                    },
                },
            },
            addressUpdateClassifier: null);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("messageField", ex.Message);
    }

    // A keywordRules 'order' that omits a map key would make that keyword set silently unreachable.
    [Fact]
    public void Validate_FailsLoud_WhenKeywordRulesOrderDoesNotCoverMapKey()
    {
        StateBackendConfiguration config = BuildIssuanceKeywordConfig(
            new KeywordRules
            {
                Order = new List<string> { "SummerEbt" }, // SnapEbtCard is mapped but not ordered
                Map = new Dictionary<string, List<string>>
                {
                    ["SummerEbt"] = new() { "OSSE" },
                    ["SnapEbtCard"] = new() { "SNAP" },
                },
                Default = "Unknown",
            });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("SnapEbtCard", ex.Message);
    }

    // A field referencing an enum table the config never defines fails at load.
    [Fact]
    public void Validate_FailsLoud_WhenReferencedEnumTableIsUndefined()
    {
        StateBackendConfiguration config = BuildEnumConfig(
            new StateBackendEnumTable
            {
                Map = new Dictionary<string, List<string>> { ["Active"] = new() { "ACTIVE" } },
                Default = "Unknown",
            }) with
        {
            Enums = null, // the field's Enum = "cardStatus" now dangles
        };

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("cardStatus", ex.Message);
    }

    // Minimal config whose household lookup maps the supplied fields.
    private static StateBackendConfiguration BuildLookupFieldsConfig(
        Dictionary<string, FieldMapping> fields) =>
        StateBackendTestConfig.Base().WithLookup(new HouseholdLookupOperationConfig
        {
            Method = StateBackendHttpMethod.Post,
            Path = "/lookup",
            Response = new StateBackendResponseMapping
            {
                Root = "$.records",
                Fields = fields,
            },
        });

    // A write-op request binding carrying a mapOptional entry.
    private static RequestBinding MapOptionalRequestBinding() =>
        new()
        {
            Map = new Dictionary<string, string> { ["caseId"] = "summerEbtCaseId" },
            MapOptional = new Dictionary<string, string> { ["reason"] = "reason" },
        };

    private static ResultClassifier SuccessClassifier() =>
        new()
        {
            Conditions = new List<ResultCondition>
            {
                new() { Outcome = WriteOutcome.Success, StatusIn = new List<int> { 200 } },
            },
            Default = WriteOutcome.BackendError,
        };

    private static RequestBinding MinimalWriteRequest() =>
        new()
        {
            Map = new Dictionary<string, string> { ["caseId"] = "summerEbtCaseId" },
        };

    // Minimal config carrying an optional card-replacement and/or address-update write op, each with
    // the supplied request binding. A dummy result classifier is attached so incompleteness
    // doesn't mask the mapOptional check.
    private static StateBackendConfiguration BuildWriteMapOptionalConfig(
        RequestBinding? cardReplacementRequest, RequestBinding? addressUpdateRequest) =>
        StateBackendTestConfig.Base() with
        {
            Operations = new StateBackendOperations
            {
                CardReplacement = cardReplacementRequest is null
                    ? null
                    : new CardReplacementOperationConfig
                    {
                        Method = StateBackendHttpMethod.Post,
                        Path = "/cards/replace",
                        Request = cardReplacementRequest,
                        Result = SuccessClassifier(),
                    },
                AddressUpdate = addressUpdateRequest is null
                    ? null
                    : new AddressUpdateOperationConfig
                    {
                        Method = StateBackendHttpMethod.Post,
                        Path = "/households/address",
                        Request = addressUpdateRequest,
                        Result = SuccessClassifier(),
                    },
            },
        };

    // A classifier whose single condition sets NONE of the three closed kinds — malformed shape.
    private static ResultClassifier MalformedClassifier() =>
        new()
        {
            Conditions = new List<ResultCondition>
            {
                new() { Outcome = WriteOutcome.Success },
            },
        };

    // Minimal config carrying an optional card-replacement and/or address-update write op, each with
    // the supplied result classifier.
    private static StateBackendConfiguration BuildWriteClassifierConfig(
        ResultClassifier? cardReplacementClassifier, ResultClassifier? addressUpdateClassifier) =>
        StateBackendTestConfig.Base() with
        {
            Operations = new StateBackendOperations
            {
                CardReplacement = cardReplacementClassifier is null
                    ? null
                    : new CardReplacementOperationConfig
                    {
                        Method = StateBackendHttpMethod.Post,
                        Path = "/cards/replace",
                        Request = MinimalWriteRequest(),
                        Result = cardReplacementClassifier,
                    },
                AddressUpdate = addressUpdateClassifier is null
                    ? null
                    : new AddressUpdateOperationConfig
                    {
                        Method = StateBackendHttpMethod.Post,
                        Path = "/households/address",
                        Request = MinimalWriteRequest(),
                        Result = addressUpdateClassifier,
                    },
            },
        };

    // Minimal config whose household lookup infers issuanceType via a keywordRules primitive.
    private static StateBackendConfiguration BuildIssuanceKeywordConfig(KeywordRules keywordRules) =>
        StateBackendTestConfig.Base().WithLookup(new HouseholdLookupOperationConfig
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
        });

    // Minimal config whose household lookup maps ebtCardStatus through a named "cardStatus" table.
    private static StateBackendConfiguration BuildEnumConfig(StateBackendEnumTable cardStatusTable) =>
        StateBackendTestConfig.Base().WithLookup(new HouseholdLookupOperationConfig
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
        }) with
        {
            Enums = new Dictionary<string, StateBackendEnumTable>
            {
                ["cardStatus"] = cardStatusTable,
            },
        };
}
