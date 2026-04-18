using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Handles starting a document verification challenge.
/// Loads the challenge by (publicId, userId) for IDOR prevention,
/// validates the state transition (must be Created → Pending),
/// calls Socure to generate a DocV session token, and updates the challenge.
/// </summary>
public class StartChallengeCommandHandler(
    IDocVerificationChallengeRepository challengeRepository,
    IUserRepository userRepository,
    ISocureClient socureClient,
    IValidator<StartChallengeCommand> validator,
    ILogger<StartChallengeCommandHandler> logger)
    : ICommandHandler<StartChallengeCommand, StartChallengeResponse>
{
    public async Task<Result<StartChallengeResponse>> Handle(
        StartChallengeCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.Validate(command, cancellationToken);
        if (validationResult is ValidationFailedResult validationFailed)
        {
            logger.LogWarning("StartChallenge validation failed for user {UserId}", command.UserId);
            return Result<StartChallengeResponse>.ValidationFailed(validationFailed.Errors);
        }

        // Load challenge scoped by ownership — returns null for wrong user (IDOR prevention)
        var challenge = await challengeRepository.GetByPublicIdAsync(
            command.ChallengeId, command.UserId, cancellationToken);

        if (challenge == null)
        {
            logger.LogWarning(
                "Challenge {ChallengeId} not found for user {UserId}",
                command.ChallengeId, command.UserId);
            return Result<StartChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.NotFound, "Challenge not found.");
        }

        // Check-on-read expiration: if Created and past ExpiresAt, transition to Expired
        if (challenge.Status == DocVerificationStatus.Created
            && challenge.ExpiresAt.HasValue
            && DateTime.UtcNow > challenge.ExpiresAt.Value)
        {
            challenge.TransitionTo(DocVerificationStatus.Expired);
            await challengeRepository.UpdateAsync(challenge, cancellationToken);

            logger.LogInformation(
                "Challenge {ChallengeId} expired on start attempt (ExpiresAt: {ExpiresAt})",
                command.ChallengeId, challenge.ExpiresAt);

            return Result<StartChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.Conflict,
                "Challenge has expired. Please re-submit your information.");
        }

        // Must be in Created state to start
        if (challenge.Status != DocVerificationStatus.Created)
        {
            // If already Pending, return the existing token (idempotent for repeated start, Codex test 6)
            if (challenge.Status == DocVerificationStatus.Pending
                && challenge.DocvTransactionToken != null
                && challenge.DocvUrl != null)
            {
                logger.LogInformation(
                    "Challenge {ChallengeId} already Pending, returning existing token for user {UserId}",
                    command.ChallengeId, command.UserId);
                return Result<StartChallengeResponse>.Success(
                    new StartChallengeResponse(challenge.DocvTransactionToken, challenge.DocvUrl));
            }

            logger.LogWarning(
                "Challenge {ChallengeId} is in {Status} state, cannot start",
                command.ChallengeId, challenge.Status);
            return Result<StartChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.Conflict,
                $"Challenge is in {challenge.Status} state and cannot be started.");
        }

        // If DocV data was stored during assessment (single-call design), use it directly
        if (challenge.DocvTransactionToken != null && challenge.DocvUrl != null)
        {
            challenge.TransitionTo(DocVerificationStatus.Pending);
            await challengeRepository.UpdateAsync(challenge, cancellationToken);

            logger.LogInformation(
                "Started DocV session for challenge {ChallengeId} using stored token, user {UserId}",
                command.ChallengeId, command.UserId);

            return Result<StartChallengeResponse>.Success(
                new StartChallengeResponse(challenge.DocvTransactionToken, challenge.DocvUrl));
        }

        // Fallback: call Socure if no stored data (e.g., stub client in dev mode).
        // The real HttpSocureClient generates DocV tokens during the assessment call and does
        // not support StartDocvSessionAsync. If we reach here with the real client, it means the
        // challenge was created without DocV data — return a clear error so the frontend can
        // redirect the user to re-submit ID proofing.
        var user = await userRepository.GetUserByIdAsync(command.UserId, cancellationToken);
        if (user == null)
        {
            return Result<StartChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.NotFound, "User not found.");
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return Result<StartChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.Conflict, "Email is required for document verification.");
        }

        Result<SocureDocvSession> sessionResult;
        try
        {
            sessionResult = await socureClient.StartDocvSessionAsync(
                command.UserId, user.Email!, cancellationToken);
        }
        catch (NotSupportedException)
        {
            logger.LogWarning(
                "Challenge {ChallengeId} has no stored DocV data and the Socure client does not " +
                "support on-demand session creation. User {UserId} must re-submit ID proofing.",
                command.ChallengeId, command.UserId);
            return Result<StartChallengeResponse>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "Document verification session expired. Please re-submit your information.");
        }

        if (!sessionResult.IsSuccess)
        {
            logger.LogWarning("Socure DocV session creation failed for user {UserId}", command.UserId);
            return Result<StartChallengeResponse>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed, "Failed to create document verification session.");
        }

        var session = sessionResult.Value;

        // Update challenge with Socure correlation fields and transition to Pending
        challenge.SocureReferenceId = session.ReferenceId;
        challenge.EvalId = session.EvalId;
        challenge.DocvTransactionToken = session.DocvTransactionToken;
        challenge.DocvUrl = session.DocvUrl;
        challenge.TransitionTo(DocVerificationStatus.Pending);

        await challengeRepository.UpdateAsync(challenge, cancellationToken);

        logger.LogInformation(
            "Started DocV session for challenge {ChallengeId}, user {UserId}",
            command.ChallengeId, command.UserId);

        return Result<StartChallengeResponse>.Success(
            new StartChallengeResponse(session.DocvTransactionToken, session.DocvUrl));
    }
}
