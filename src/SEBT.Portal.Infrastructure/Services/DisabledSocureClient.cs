using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.Infrastructure.Services;

/// <summary>
/// No-op implementation of <see cref="ISocureClient"/> used when Socure integration is disabled.
/// Returns a <see cref="DependencyFailedReason.NotConfigured"/> result for all operations.
/// </summary>
public class DisabledSocureClient : ISocureClient
{
    private const string DisabledMessage = "Socure integration is not enabled for this deployment.";

    public Task<Result<IdProofingAssessmentResult>> RunIdProofingAssessmentAsync(
        Guid userId,
        string email,
        string dateOfBirth,
        string? idType,
        string? idValue,
        string? ipAddress = null,
        string? phoneNumber = null,
        string? givenName = null,
        string? familyName = null,
        Address? address = null,
        string? diSessionToken = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Result<IdProofingAssessmentResult>.DependencyFailed(
                DependencyFailedReason.NotConfigured, DisabledMessage));
    }

    public Task<Result<SocureDocvSession>> StartDocvSessionAsync(
        Guid userId,
        string email,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Result<SocureDocvSession>.DependencyFailed(
                DependencyFailedReason.NotConfigured, DisabledMessage));
    }
}
