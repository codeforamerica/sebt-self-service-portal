using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// Runs address verification against simulated Smarty outcomes so diagnostic endpoints can
/// exercise the verification service's error-handling and logging paths without any network
/// activity.
/// </summary>
public interface IAddressVerificationDiagnostics
{
    /// <summary>
    /// Verifies a sample address against a canned successful Smarty response carrying the
    /// given JSON body, returning the verification outcome.
    /// </summary>
    Task<Result<AddressUpdateSuccess>> ValidateAgainstCannedSuccessAsync(
        string responseBody,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a sample address against a canned Smarty server-error response,
    /// returning the verification outcome.
    /// </summary>
    Task<Result<AddressUpdateSuccess>> ValidateAgainstCannedServerErrorAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies a sample address against a simulated transport-level failure
    /// (e.g. firewall block, DNS failure), returning the verification outcome.
    /// </summary>
    Task<Result<AddressUpdateSuccess>> ValidateAgainstTransportFailureAsync(
        CancellationToken cancellationToken = default);
}
