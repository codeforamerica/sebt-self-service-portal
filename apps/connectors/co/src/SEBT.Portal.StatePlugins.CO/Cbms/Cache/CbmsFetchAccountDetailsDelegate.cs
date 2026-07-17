using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

/// <summary>
/// Captures the CBMS get-account-details call so the cache can be tested
/// without instantiating a real Kiota client. Returns null when CBMS reports
/// no household for the given normalized phone (404 or empty rows).
/// </summary>
internal delegate Task<GetAccountDetailsResponse?> CbmsFetchAccountDetailsDelegate(
    string normalizedPhone,
    bool includeCardService,
    CancellationToken cancellationToken);
