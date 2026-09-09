using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Default <see cref="IBlockedAddressDataSource"/> for states that rely solely on
/// the inline <c>AddressValidationData.BlockedAddresses</c> list (e.g., DC). Returns
/// no entries; the legacy L1-only matcher is sufficient for small hand-curated lists.
/// </summary>
public sealed class EmptyBlockedAddressDataSource : IBlockedAddressDataSource
{
    public IReadOnlyCollection<BlockedAddressEntry> GetEntries() => [];
}
