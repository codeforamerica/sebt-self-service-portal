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

        Assert.NotNull(request.Constants);
        Assert.Equal(false, request.Constants["includePendingApplicantDetails"]);

        // isIdentityProofed must be a per-request map pass-through, never a constant — hardcoding it
        // would bypass the DC lookup's proofing gate.
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

        // The wrapper passes DC's raw column names through and serializes DATE columns as ISO 8601.
        FieldMapping issueDate = response.Fields["ebtCardIssueDate"];
        Assert.Equal("EbtCardIssueDate", issueDate.From);
        Assert.Equal("yyyy-MM-ddTHH:mm:ss", issueDate.Format);

        FieldMapping cardStatus = response.Fields["ebtCardStatus"];
        Assert.Equal("EbtCardStatus", cardStatus.From);
        Assert.Equal("cardStatus", cardStatus.Enum);

        FieldMapping issuanceType = response.Fields["issuanceType"];
        Assert.Equal(new[] { "HouseholdType", "EligibilityType" }, issuanceType.From.All);
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

        // The lookup response never echoes the household email DC's writes bind, so the token
        // packs it from the lookup's caller context.
        Assert.NotNull(caseId.FromContext);
        Assert.Equal("householdIdentifier", caseId.FromContext["householdEmail"]);

        CardReplacementOperationConfig? cardReplacement = config.Operations.CardReplacement;
        Assert.NotNull(cardReplacement);
        Assert.Equal(StateBackendHttpMethod.Post, cardReplacement.Method);
        Assert.Equal("/card-replacements", cardReplacement.Path);

        // The wrapper's request model is the sproc's three inputs; the canonical contract carries
        // no replacement reason, so only the two token-carried routing fields bind.
        Assert.NotNull(cardReplacement.Request);
        Assert.Null(cardReplacement.Request.Constants);
        Assert.Equal("summerEbtCaseId", cardReplacement.Request.Map!["caseId"]);
        Assert.Equal("householdEmail", cardReplacement.Request.Map["householdEmail"]);

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

        // DC address update uses the SHARED batch shape (one household field across all cases).
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
        Assert.Equal(WriteOutcome.Success, dcAddressSuccess.Outcome);
        Assert.Equal("resultCode", dcAddressSuccess.Field);
        Assert.Equal(new[] { "OK" }, dcAddressSuccess.ValueIn);
        Assert.Equal(WriteOutcome.BackendError, dcAddressClassifier.Default);

        // DC enrollment uses PerChild fan-out (no index, no expansion).
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

        // schoolIdentifier is optional: the portal may not carry it, but DC's match reads it when sent.
        Assert.NotNull(dcEnrollment.Request.MapOptional);
        Assert.Equal("schoolName", dcEnrollment.Request.MapOptional["schoolIdentifier"]);

        Assert.NotNull(dcEnrollment.Response);
        Assert.Equal("$", dcEnrollment.Response.Root);
        Assert.Null(dcEnrollment.Response.IndexField);
        Assert.Equal(EnrollmentMatchStrategy.AnyRowValueIn, dcEnrollment.Response.Match.Strategy);
        Assert.Equal("isEligible", dcEnrollment.Response.Match.Field);
        Assert.Equal(new[] { "true" }, dcEnrollment.Response.Match.ValueIn);

        // DC's backend never reported per-row or result-level messages — carriers stay unconfigured.
        Assert.Null(dcEnrollment.Response.StatusMessageField);
        Assert.Null(dcEnrollment.Response.MessageField);

        Assert.NotNull(config.Operations.Health);

        // Capabilities derive from which operations the config declares.
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

        // Covers the client_credentials auth branch (DC covers api_key).
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

        // Covers the valueInSet disaggregation branch (DC covers presence).
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

        // CO uses the COLLECT batch shape: per-case write-ids into an array (DC uses shared).
        Assert.NotNull(addressUpdate.Request);
        Assert.Equal("cases", addressUpdate.Request.Collect!["writeId"]);
        Assert.Null(addressUpdate.Request.Shared);
        Assert.Equal("stdAddr", addressUpdate.Request.Map!["line1"]);
        Assert.Equal("stdZip", addressUpdate.Request.Map["zip"]);

        ResultClassifier coAddressClassifier = Assert.IsType<ResultClassifier>(addressUpdate.Result);
        ResultCondition coAddressSuccess = Assert.Single(coAddressClassifier.Conditions);
        Assert.Equal(WriteOutcome.Success, coAddressSuccess.Outcome);
        Assert.Equal("respCd", coAddressSuccess.Field);
        Assert.Equal(new[] { "200", "00" }, coAddressSuccess.ValueIn);

        // CO enrollment uses batch + transposeMonthDay expansion + confidenceThreshold match.
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

        // schoolIdentifier is optional: the portal may not carry it, but CO's match reads it when sent.
        Assert.NotNull(coEnrollment.Request.MapOptional);
        Assert.Equal("StdSchlCd", coEnrollment.Request.MapOptional["schoolIdentifier"]);

        Assert.NotNull(coEnrollment.Response);
        Assert.Equal("$.stdntDtls", coEnrollment.Response.Root);
        Assert.Equal("stdReqInd", coEnrollment.Response.IndexField);

        // CO surfaces the winning row's eligibility text per child and CBMS's root RespMsg
        // result-level — the carriers the plugin exposed on the wire.
        Assert.Equal("sebtEligSts", coEnrollment.Response.StatusMessageField);
        Assert.Equal("RespMsg", coEnrollment.Response.MessageField);
        Assert.Equal(EnrollmentMatchStrategy.ConfidenceThreshold, coEnrollment.Response.Match.Strategy);
        Assert.Equal("mtchCnfd", coEnrollment.Response.Match.ScoreField);
        Assert.Equal(90.0, coEnrollment.Response.Match.Threshold);

        // CO's real rule also requires the best row's eligibility flag — not score alone.
        Assert.Equal("sebtEligSts", coEnrollment.Response.Match.Field);
        Assert.Equal(new[] { "Y" }, coEnrollment.Response.Match.ValueIn);

        StateBackendCapabilities capabilities = config.Capabilities;
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

    // A malformed card-replacement classifier (a condition setting no closed kind) fails loud at load.
    [Fact]
    public void Validate_FailsLoud_WhenCardReplacementClassifierConditionSetsNoKind()
    {
        StateBackendConfiguration config = BuildWriteClassifierConfig(
            cardReplacementClassifier: MalformedClassifier(), addressUpdateClassifier: null);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("exactly one", ex.Message);
    }

    // The validator checks BOTH write ops: the same malformed classifier on address-update fails too.
    [Fact]
    public void Validate_FailsLoud_WhenAddressUpdateClassifierConditionSetsNoKind()
    {
        StateBackendConfiguration config = BuildWriteClassifierConfig(
            cardReplacementClassifier: null, addressUpdateClassifier: MalformedClassifier());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("exactly one", ex.Message);
    }

    // The write-path body builders ignore mapOptional, so a config setting it must fail at load
    // rather than silently dropping the binding.
    [Fact]
    public void Validate_FailsLoud_WhenCardReplacementRequestSetsMapOptional()
    {
        StateBackendConfiguration config = BuildWriteMapOptionalConfig(
            cardReplacementRequest: MapOptionalRequestBinding(), addressUpdateRequest: null);

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("mapOptional", ex.Message);
    }

    [Fact]
    public void Validate_FailsLoud_WhenAddressUpdateRequestSetsMapOptional()
    {
        StateBackendConfiguration config = BuildWriteMapOptionalConfig(
            cardReplacementRequest: null, addressUpdateRequest: MapOptionalRequestBinding());

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("mapOptional", ex.Message);
    }

    // A fromContext entry referencing a context name outside the closed vocabulary fails at load —
    // context names are resolved in fixed code, never expressions.
    [Fact]
    public void Validate_FailsLoud_WhenCaseIdFromContextNameIsUnknown()
    {
        StateBackendConfiguration config = BuildCaseIdConfig(new CaseIdComposition
        {
            Fields = new Dictionary<string, string> { ["caseId"] = "SummerEBTCaseID" },
            FromContext = new Dictionary<string, string> { ["householdEmail"] = "guardianEmail" },
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("guardianEmail", ex.Message);
    }

    // The same token field sourced from BOTH a response column and caller context is ambiguous.
    [Fact]
    public void Validate_FailsLoud_WhenCaseIdFieldIsInBothFieldsAndFromContext()
    {
        StateBackendConfiguration config = BuildCaseIdConfig(new CaseIdComposition
        {
            Fields = new Dictionary<string, string> { ["householdEmail"] = "GuardianEmail" },
            FromContext = new Dictionary<string, string> { ["householdEmail"] = "householdIdentifier" },
        });

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(
            () => StateBackendConfigurationValidator.Validate(config));
        Assert.Contains("householdEmail", ex.Message);
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
                        Fields = fields,
                    },
                },
            },
        };

    // Minimal config whose household lookup composes caseId tokens with the supplied brick.
    private static StateBackendConfiguration BuildCaseIdConfig(CaseIdComposition caseId) =>
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
                            ["childFirstName"] = new() { From = "ChildFirstName" },
                        },
                        CaseId = caseId,
                    },
                },
            },
        };

    // A write-op request binding carrying a mapOptional entry — unsupported on the write path.
    private static RequestBinding MapOptionalRequestBinding() =>
        new()
        {
            Map = new Dictionary<string, string> { ["caseId"] = "summerEbtCaseId" },
            MapOptional = new Dictionary<string, string> { ["reason"] = "reason" },
        };

    // Minimal config carrying an optional card-replacement and/or address-update write op, each with
    // the supplied request binding.
    private static StateBackendConfiguration BuildWriteMapOptionalConfig(
        RequestBinding? cardReplacementRequest, RequestBinding? addressUpdateRequest) =>
        new()
        {
            BaseUrl = new Uri("http://backend.test"),
            Auth = new StateBackendApiKeyAuthScheme { Header = "X-Api-Key", KeyRef = "k" },
            Operations = new StateBackendOperations
            {
                CardReplacement = cardReplacementRequest is null
                    ? null
                    : new CardReplacementOperationConfig
                    {
                        Method = StateBackendHttpMethod.Post,
                        Path = "/cards/replace",
                        Request = cardReplacementRequest,
                    },
                AddressUpdate = addressUpdateRequest is null
                    ? null
                    : new AddressUpdateOperationConfig
                    {
                        Method = StateBackendHttpMethod.Post,
                        Path = "/households/address",
                        Request = addressUpdateRequest,
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
        new()
        {
            BaseUrl = new Uri("http://backend.test"),
            Auth = new StateBackendApiKeyAuthScheme { Header = "X-Api-Key", KeyRef = "k" },
            Operations = new StateBackendOperations
            {
                CardReplacement = cardReplacementClassifier is null
                    ? null
                    : new CardReplacementOperationConfig
                    {
                        Method = StateBackendHttpMethod.Post,
                        Path = "/cards/replace",
                        Result = cardReplacementClassifier,
                    },
                AddressUpdate = addressUpdateClassifier is null
                    ? null
                    : new AddressUpdateOperationConfig
                    {
                        Method = StateBackendHttpMethod.Post,
                        Path = "/households/address",
                        Result = addressUpdateClassifier,
                    },
            },
        };

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
