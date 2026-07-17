using SEBT.Portal.Core.Models.Household;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Supplies blocked-address entries with ZIP context, sourced from per-state data
/// files (CSV, embedded resource, etc.) that ship alongside the application.
/// Distinct from <c>AddressValidationDataSettings.BlockedAddresses</c>, which is
/// a legacy inline list matched on street alone.
///
/// Entries returned here participate in (street + 5-digit ZIP) matching so that
/// large state-wide lists do not over-block residential addresses that happen to
/// share a street name with a county office in another city.
/// </summary>
public interface IBlockedAddressDataSource
{
    IReadOnlyCollection<BlockedAddressEntry> GetEntries();
}
