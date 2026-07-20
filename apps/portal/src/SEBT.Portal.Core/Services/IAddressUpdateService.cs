using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Core.Services;

/// <summary>
/// Validates and normalizes mailing addresses using Smarty (when enabled) and state policy from configuration.
/// Intended for use by portal use cases and state connectors so validation rules stay consistent.
/// </summary>
public interface IAddressUpdateService
{
    /// <summary>
    /// Validates the address with Smarty (or pass-through when disabled), applies per-state policy
    /// (e.g. General Delivery), and returns a normalized address or structured validation errors.
    /// </summary>
    Task<Result<AddressUpdateSuccess>> ValidateAndNormalizeAsync(
        AddressUpdateOperationRequest request,
        CancellationToken cancellationToken = default);
}
