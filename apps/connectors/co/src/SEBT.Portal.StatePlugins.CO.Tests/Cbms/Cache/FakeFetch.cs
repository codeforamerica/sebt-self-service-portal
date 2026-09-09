using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

internal sealed class FakeFetch
{
    public int CallCount { get; set; }
    public GetAccountDetailsResponse? NextResponse { get; set; }
    public bool ThrowOnNext { get; set; }

    public CbmsFetchAccountDetailsDelegate Delegate => (_, _, _) =>
    {
        CallCount++;
        if (ThrowOnNext) throw new InvalidOperationException("Simulated CBMS failure");
        return Task.FromResult(NextResponse);
    };
}
