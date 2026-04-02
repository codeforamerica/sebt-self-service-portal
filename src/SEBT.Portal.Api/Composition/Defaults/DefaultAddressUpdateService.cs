using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models.Household;

namespace SEBT.Portal.Api.Composition.Defaults;

/// <summary>
/// Default implementation when no state-specific IAddressUpdateService plugin is loaded.
/// Returns a backend error indicating the service is not configured.
/// </summary>
internal class DefaultAddressUpdateService : IAddressUpdateService
{
    public Task<AddressUpdateResult> UpdateAddressAsync(
        AddressUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            AddressUpdateResult.BackendError("NOT_CONFIGURED", "No address update service configured."));
    }
}
