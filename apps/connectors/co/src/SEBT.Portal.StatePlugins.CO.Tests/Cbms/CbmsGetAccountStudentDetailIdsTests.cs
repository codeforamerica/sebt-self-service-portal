using System.Text.Json;
using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms;

public class CbmsGetAccountStudentDetailIdsTests
{
    [Fact]
    public void Resolve_uses_typed_properties_when_present()
    {
        var row = new GetAccountStudentDetail { SebtChldId = 101, SebtAppId = 202 };
        var ids = CbmsGetAccountStudentDetailIds.Resolve(row);
        Assert.Equal("101", ids.SebtChldId);
        Assert.Equal("202", ids.SebtAppId);
        Assert.True(CbmsGetAccountStudentDetailIds.CanBuildUpdatePayload(ids));
    }

    [Fact]
    public void Resolve_reads_PascalCase_sebtChldId_from_AdditionalData()
    {
        var row = new GetAccountStudentDetail
        {
            AdditionalData = new Dictionary<string, object> { ["SebtChldId"] = "C-from-additional" }
        };
        var ids = CbmsGetAccountStudentDetailIds.Resolve(row);
        Assert.Equal("C-from-additional", ids.SebtChldId);
        Assert.True(CbmsGetAccountStudentDetailIds.CanBuildUpdatePayload(ids));
    }

    [Fact]
    public void Resolve_reads_JsonElement_string_from_AdditionalData()
    {
        using var doc = JsonDocument.Parse("""{"SebtChldId":"C-json"}""");
        var el = doc.RootElement.GetProperty("SebtChldId");
        var row = new GetAccountStudentDetail
        {
            AdditionalData = new Dictionary<string, object> { ["SebtChldId"] = el }
        };
        var ids = CbmsGetAccountStudentDetailIds.Resolve(row);
        Assert.Equal("C-json", ids.SebtChldId);
    }

    [Fact]
    public void CanBuildUpdatePayload_true_when_only_sebtAppId()
    {
        var row = new GetAccountStudentDetail { SebtAppId = 999 };
        var ids = CbmsGetAccountStudentDetailIds.Resolve(row);
        Assert.Null(ids.SebtChldId);
        Assert.Equal("999", ids.SebtAppId);
        Assert.True(CbmsGetAccountStudentDetailIds.CanBuildUpdatePayload(ids));
    }

    [Fact]
    public void CanBuildUpdatePayload_false_when_both_missing()
    {
        var row = new GetAccountStudentDetail { StdFstNm = "Pat" };
        var ids = CbmsGetAccountStudentDetailIds.Resolve(row);
        Assert.False(CbmsGetAccountStudentDetailIds.CanBuildUpdatePayload(ids));
    }

    [Fact]
    public void Resolve_matches_AdditionalData_keys_case_insensitively()
    {
        var row = new GetAccountStudentDetail
        {
            AdditionalData = new Dictionary<string, object> { ["SEBTCHLDID"] = "upper-child" }
        };
        var ids = CbmsGetAccountStudentDetailIds.Resolve(row);
        Assert.Equal("upper-child", ids.SebtChldId);
    }

    [Fact]
    public void Resolve_heuristic_finds_sebt_prefixed_chld_id_key()
    {
        var row = new GetAccountStudentDetail
        {
            AdditionalData = new Dictionary<string, object> { ["vendor_sebt_x_chld_id"] = "H-1" }
        };
        var ids = CbmsGetAccountStudentDetailIds.Resolve(row);
        Assert.Equal("H-1", ids.SebtChldId);
    }

    [Fact]
    public void Resolve_does_not_treat_approvalId_as_sebt_app_id()
    {
        var row = new GetAccountStudentDetail
        {
            AdditionalData = new Dictionary<string, object> { ["approvalId"] = "APR-1" }
        };
        Assert.False(CbmsGetAccountStudentDetailIds.CanBuildUpdatePayload(CbmsGetAccountStudentDetailIds.Resolve(row)));
    }

    [Fact]
    public void FormatDiagnosticsHint_lists_unmapped_property_names()
    {
        var row = new GetAccountStudentDetail
        {
            AdditionalData = new Dictionary<string, object> { ["SebtChildNumber"] = "1" }
        };
        var hint = CbmsGetAccountStudentDetailIds.FormatDiagnosticsHint(row);
        Assert.Contains("SebtChildNumber", hint);
    }

    [Fact]
    public void FormatDiagnosticsHint_when_no_extra_json_lists_other_populated_fields()
    {
        var row = new GetAccountStudentDetail { StdFstNm = "A", StdLstNm = "B" };
        var hint = CbmsGetAccountStudentDetailIds.FormatDiagnosticsHint(row);
        Assert.Contains("StdFstNm", hint);
        Assert.Contains("StdLstNm", hint);
        Assert.Contains("SebtChldId", hint);
        Assert.Contains("These correlation fields were empty", hint);
    }
}
