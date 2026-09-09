using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;

namespace SEBT.Portal.StatePlugins.CO.Cbms.Cache;

internal interface ICbmsHouseholdCache
{
    /// <summary>
    /// Returns the cached CBMS GetAccountDetailsResponse for the household,
    /// fetching from CBMS on miss or hard-expiry. On soft-expiry, returns the
    /// cached value AND triggers a coalesced background refresh.
    /// Returns null when CBMS reports no household for the normalized phone.
    /// </summary>
    Task<GetAccountDetailsResponse?> GetAsync(string normalizedPhone, bool includeCardService, CancellationToken cancellationToken);

    /// <summary>
    /// Write-through: store the (locally-mutated) response after a successful PATCH.
    /// On underlying SetAsync failure, falls back to InvalidateAsync (tripwire).
    /// </summary>
    Task SetAsync(string normalizedPhone, GetAccountDetailsResponse value, CancellationToken cancellationToken);

    /// <summary>
    /// Explicit invalidation. Used by the tripwire and as an escape hatch.
    /// </summary>
    Task InvalidateAsync(string normalizedPhone, CancellationToken cancellationToken);
}
