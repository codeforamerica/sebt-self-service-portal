using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Handles querying the verification status of a challenge.
/// Performs check-on-read expiration: if a Pending challenge is past its ExpiresAt,
/// it is transitioned to Expired before responding.
/// </summary>
public class GetVerificationStatusQueryHandler(
    IDocVerificationChallengeRepository challengeRepository,
    ILogger<GetVerificationStatusQueryHandler> logger)
    : IQueryHandler<GetVerificationStatusQuery, VerificationStatusResponse>
{
    public async Task<Result<VerificationStatusResponse>> Handle(
        GetVerificationStatusQuery query,
        CancellationToken cancellationToken = default)
    {
        var challenge = await challengeRepository.GetByPublicIdAsync(
            query.ChallengeId, query.UserId, cancellationToken);

        if (challenge == null)
        {
            logger.LogWarning(
                "Verification status query: challenge {ChallengeId} not found for user {UserId}",
                query.ChallengeId, query.UserId);
            return Result<VerificationStatusResponse>.PreconditionFailed(
                PreconditionFailedReason.NotFound, "Challenge not found.");
        }

        // Check-on-read expiration: if Created or Pending and past ExpiresAt, transition to Expired
        if ((challenge.Status == DocVerificationStatus.Created
             || challenge.Status == DocVerificationStatus.Pending)
            && challenge.ExpiresAt.HasValue
            && DateTime.UtcNow > challenge.ExpiresAt.Value)
        {
            challenge.TransitionTo(DocVerificationStatus.Expired);
            await challengeRepository.UpdateAsync(challenge, cancellationToken);

            logger.LogInformation(
                "Challenge {ChallengeId} expired on read (ExpiresAt: {ExpiresAt})",
                query.ChallengeId, challenge.ExpiresAt);
        }

        var statusString = challenge.Status switch
        {
            DocVerificationStatus.Created => "pending",
            DocVerificationStatus.Pending => "pending",
            DocVerificationStatus.Verified => "verified",
            DocVerificationStatus.Rejected => "rejected",
            DocVerificationStatus.Expired => "rejected",
            _ => "pending"
        };

        return Result<VerificationStatusResponse>.Success(
            new VerificationStatusResponse(
                statusString,
                AllowIdRetry: challenge.AllowIdRetry,
                OffboardingReason: challenge.OffboardingReason));
    }
}
