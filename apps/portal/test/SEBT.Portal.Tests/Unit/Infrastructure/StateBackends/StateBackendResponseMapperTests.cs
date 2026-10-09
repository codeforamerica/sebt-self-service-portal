using System.Text.Json;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends.Configuration;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;
using SEBT.Portal.Tests.Unit.Infrastructure.StateBackends.ConfigSamples;

namespace SEBT.Portal.Tests.Unit.Infrastructure.StateBackends;

/// <summary>
/// Focused mapping-engine tests: JSON number coercion, keyword first-match, and incomplete
/// disaggregation — the cases later driver tests don't isolate.
/// </summary>
public class StateBackendResponseMapperTests
{
    [Fact]
    public void MapHousehold_CoercesNumericIds_ToStringFields()
    {
        StateBackendConfiguration configuration = LookupFields(new Dictionary<string, FieldMapping>
        {
            ["summerEBTCaseID"] = new() { From = "sebtChldCwin" },
            ["applicationId"] = new() { From = "sebtAppId" },
        });

        using JsonDocument document = JsonDocument.Parse(
            """{ "records": [ { "sebtChldCwin": 1001, "sebtAppId": 0 } ] }""");

        HouseholdData household = StateBackendResponseMapper.MapHousehold(
            document.RootElement,
            configuration,
            configuration.Operations.HouseholdLookup!.Response!);

        SummerEbtCase mapped = Assert.Single(household.SummerEbtCases);
        Assert.Equal("1001", mapped.SummerEBTCaseID);
        Assert.Equal("0", mapped.ApplicationId);
    }

    [Fact]
    public void MapHousehold_KeywordRules_AreCaseInsensitive_AndFirstMatchWins()
    {
        StateBackendConfiguration configuration = LookupFields(new Dictionary<string, FieldMapping>
        {
            ["summerEBTCaseID"] = new() { From = "id" },
            ["issuanceType"] = new()
            {
                From = new[] { "HouseholdType", "EligibilityType" },
                KeywordRules = new KeywordRules
                {
                    Order = new List<string> { "SummerEbt", "SnapEbtCard" },
                    Map = new Dictionary<string, List<string>>
                    {
                        ["SummerEbt"] = new() { "OSSE", "NSLP" },
                        ["SnapEbtCard"] = new() { "FOOD", "SNAP" },
                    },
                    Default = "Unknown",
                },
            },
        });

        using JsonDocument document = JsonDocument.Parse(
            """
            { "records": [
                { "id": "1", "HouseholdType": "osse enrolled", "EligibilityType": "snap household" },
                { "id": "2", "HouseholdType": "standard", "EligibilityType": "Food assistance" }
            ] }
            """);

        HouseholdData household = StateBackendResponseMapper.MapHousehold(
            document.RootElement,
            configuration,
            configuration.Operations.HouseholdLookup!.Response!);

        Assert.Equal(IssuanceType.SummerEbt, household.SummerEbtCases[0].IssuanceType);
        Assert.Equal(IssuanceType.SnapEbtCard, household.SummerEbtCases[1].IssuanceType);
    }

    [Fact]
    public void MapHousehold_MapsBenefitExpirationDate()
    {
        StateBackendConfiguration configuration = LookupFields(new Dictionary<string, FieldMapping>
        {
            ["summerEBTCaseID"] = new() { From = "id" },
            ["benefitExpirationDate"] = new() { From = "benExpDt", Format = "yyyy-MM-dd" },
        });

        using JsonDocument document = JsonDocument.Parse(
            """{ "records": [ { "id": "1", "benExpDt": "2026-08-05" } ] }""");

        HouseholdData household = StateBackendResponseMapper.MapHousehold(
            document.RootElement,
            configuration,
            configuration.Operations.HouseholdLookup!.Response!);

        SummerEbtCase mapped = Assert.Single(household.SummerEbtCases);
        Assert.Equal(new DateTime(2026, 8, 5), mapped.BenefitExpirationDate);
    }

    [Fact]
    public void MapHousehold_BlankDateString_LeavesTheDateUnset()
    {
        StateBackendConfiguration configuration = LookupFields(new Dictionary<string, FieldMapping>
        {
            ["summerEBTCaseID"] = new() { From = "id" },
            ["benefitExpirationDate"] = new() { From = "benExpDt", Format = "yyyy-MM-dd" },
        });

        using JsonDocument document = JsonDocument.Parse(
            """{ "records": [ { "id": "1", "benExpDt": "" } ] }""");

        HouseholdData household = StateBackendResponseMapper.MapHousehold(
            document.RootElement,
            configuration,
            configuration.Operations.HouseholdLookup!.Response!);

        SummerEbtCase mapped = Assert.Single(household.SummerEbtCases);
        Assert.Null(mapped.BenefitExpirationDate);
    }

    [Fact]
    public void MapHousehold_ValueInSet_IsCaseInsensitive()
    {
        StateBackendConfiguration configuration = StateBackendTestConfig.Base().WithLookup(
            new HouseholdLookupOperationConfig
            {
                Method = StateBackendHttpMethod.Post,
                Path = "/lookup",
                Response = new StateBackendResponseMapping
                {
                    Root = "$.records",
                    Fields = new Dictionary<string, FieldMapping>
                    {
                        ["summerEBTCaseID"] = new() { From = "id" },
                        ["applicationStatus"] = new() { From = "status", Enum = "applicationStatus" },
                    },
                    Disaggregation = new StateBackendDisaggregation
                    {
                        Rule = DisaggregationRule.ValueInSet,
                        DiscriminatorField = "eligSrc",
                        ApplicationValues = new List<string> { "CBMS", "PK" },
                        GroupApplicationsBy = "appId",
                        CaseInclusion = CaseInclusionPredicate.WhenApprovedOrNotApplicationBased,
                    },
                },
            }) with
        {
            Enums = new Dictionary<string, StateBackendEnumTable>
            {
                ["applicationStatus"] = new()
                {
                    Map = new Dictionary<string, List<string>> { ["Approved"] = new() { "AP" } },
                    Default = "Unknown",
                },
            },
        };

        using JsonDocument document = JsonDocument.Parse(
            """
            { "records": [
                { "id": "1", "eligSrc": "cbms", "status": "AP", "appId": "A-1" },
                { "id": "2", "eligSrc": "DIRC", "status": "AP" }
            ] }
            """);

        HouseholdData household = StateBackendResponseMapper.MapHousehold(
            document.RootElement,
            configuration,
            configuration.Operations.HouseholdLookup!.Response!);

        Assert.Equal(2, household.SummerEbtCases.Count);
        Assert.Equal("A-1", household.SummerEbtCases[0].ApplicationId);
        Assert.Null(household.SummerEbtCases[1].ApplicationId);
        Assert.Equal("A-1", Assert.Single(household.Applications).ApplicationNumber);
    }

    [Fact]
    public void MapHousehold_ClientCredentialsSample_MapsConfiguredLookupFields()
    {
        StateBackendConfiguration configuration =
            StateBackendConfigurationLoader.Load(SampleLoader.Load("co.sample.yaml"));

        using JsonDocument document = JsonDocument.Parse(
            """
            {
              "stdntEnrollDtls": [
                {
                  "sebtChldCwin": 1001,
                  "stdFstNm": "Sophia",
                  "stdLstNm": "Martinez",
                  "stdDob": "2016-04-02",
                  "stdntEligSts": "AP",
                  "eligSrc": "CBMS",
                  "sebtAppId": 55,
                  "sebtChldId": 9,
                  "cbmsCsId": "CBMS-1",
                  "ebtCardLastFour": "4422",
                  "ebtCardSts": "ACTIVE",
                  "cardIssDt": "2026-06-01",
                  "cardBal": 120.5,
                  "benAvalDt": "2026-06-15",
                  "benExpDt": "2026-10-15",
                  "addrLn1": "100 Oak Street",
                  "addrLn2": "Suite 100",
                  "cty": "Springfield",
                  "staCd": "IL",
                  "zip": "62701",
                  "zip4": "1234"
                },
                {
                  "sebtChldCwin": 1002,
                  "stdFstNm": "Grace",
                  "stdLstNm": "Hopper",
                  "eligSrc": "SCHOOL"
                }
              ]
            }
            """);

        HouseholdData household = StateBackendResponseMapper.MapHousehold(
            document.RootElement,
            configuration,
            configuration.Operations.HouseholdLookup!.Response!);

        SummerEbtCase sophia = household.SummerEbtCases[0];
        IReadOnlyDictionary<string, string> sophiaIds = OpaqueCaseId.Decode(sophia.SummerEBTCaseID!);
        Assert.Equal("9", sophiaIds["sebtChldId"]);
        Assert.Equal("55", sophiaIds["sebtAppId"]);
        Assert.Equal("Sophia", sophia.ChildFirstName);
        Assert.Equal("Martinez", sophia.ChildLastName);
        Assert.Equal(new DateTime(2016, 4, 2), sophia.ChildDateOfBirth);
        Assert.Equal("SEBT", sophia.HouseholdType);
        Assert.Equal("AP", sophia.EligibilityType);
        Assert.Equal(IssuanceType.SummerEbt, sophia.IssuanceType);
        Assert.Equal(ApplicationStatus.Approved, sophia.ApplicationStatus);
        Assert.Equal("9", sophia.ApplicationStudentId);
        Assert.Equal("55", sophia.ApplicationId);
        Assert.Equal("CBMS-1", sophia.EbtCaseNumber);
        Assert.Equal("55", sophia.CaseDisplayNumber);
        Assert.Equal("4422", sophia.EbtCardLastFour);
        Assert.Equal(CardStatus.Active, sophia.EbtCardStatus);
        Assert.Equal(new DateTime(2026, 6, 1), sophia.EbtCardIssueDate);
        Assert.Equal(120.5m, sophia.EbtCardBalance);
        Assert.Equal(new DateTime(2026, 6, 15), sophia.BenefitAvailableDate);
        Assert.Equal(new DateTime(2026, 10, 15), sophia.BenefitExpirationDate);
        Assert.False(sophia.IsStreamlineCertified);
        Assert.Equal("100 Oak Street", sophia.MailingAddress!.StreetAddress1);
        Assert.Equal("Suite 100", sophia.MailingAddress.StreetAddress2);
        Assert.Equal("Springfield", sophia.MailingAddress.City);
        Assert.Equal("IL", sophia.MailingAddress.State);
        Assert.Equal("62701-1234", sophia.MailingAddress.PostalCode);
        Assert.Equal("62701-1234", household.AddressOnFile!.PostalCode);

        SummerEbtCase grace = household.SummerEbtCases[1];
        Assert.Equal("Grace", grace.ChildFirstName);
        Assert.Equal("Hopper", grace.ChildLastName);
        Assert.Equal(IssuanceType.SummerEbt, grace.IssuanceType);
        Assert.True(grace.IsStreamlineCertified);
        Assert.Null(grace.MailingAddress);
    }

    private static StateBackendConfiguration LookupFields(Dictionary<string, FieldMapping> fields) =>
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
}
