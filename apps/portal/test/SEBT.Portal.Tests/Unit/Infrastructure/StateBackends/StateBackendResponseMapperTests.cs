using System.Text.Json;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;

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
            configuration.Operations.HouseholdLookup!.Response!,
            new CaseIdContext());

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
            configuration.Operations.HouseholdLookup!.Response!,
            new CaseIdContext());

        Assert.Equal(IssuanceType.SummerEbt, household.SummerEbtCases[0].IssuanceType);
        Assert.Equal(IssuanceType.SnapEbtCard, household.SummerEbtCases[1].IssuanceType);
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
            configuration.Operations.HouseholdLookup!.Response!,
            new CaseIdContext());

        Assert.Equal(2, household.SummerEbtCases.Count);
        Assert.Equal("A-1", household.SummerEbtCases[0].ApplicationId);
        Assert.Null(household.SummerEbtCases[1].ApplicationId);
        Assert.Equal("A-1", Assert.Single(household.Applications).ApplicationNumber);
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
