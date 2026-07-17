using SEBT.Portal.StatePlugins.CO.Cbms;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms;

public class CbmsCaseFiltersTests
{
    [Theory]
    [InlineData("DD")]
    [InlineData("dd")]
    [InlineData("Dd")]
    [InlineData("dD")]
    public void IsDeniedDuplicate_returns_true_for_DD_codes_case_insensitive(string code)
    {
        var row = new GetAccountStudentDetail { StdntEligSts = code };

        Assert.True(CbmsCaseFilters.IsDeniedDuplicate(row));
    }

    [Theory]
    [InlineData("AP")]
    [InlineData("DE")]
    [InlineData("OT")]
    [InlineData("AI")]
    [InlineData("PD")]
    [InlineData("PE")]
    [InlineData("PG")]
    [InlineData("PS")]
    [InlineData("")]
    [InlineData(null)]
    public void IsDeniedDuplicate_returns_false_for_non_DD_codes(string? code)
    {
        var row = new GetAccountStudentDetail { StdntEligSts = code };

        Assert.False(CbmsCaseFilters.IsDeniedDuplicate(row));
    }
}
