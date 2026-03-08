using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Handles an incoming Socure webhook. Validates the signature (placeholder in dev),
/// checks idempotency via event_id (D8), correlates to a challenge via ReferenceId/EvalId (D6),
/// validates state transition (D7), and updates both challenge and user state on verification.
/// </summary>
public class ProcessWebhookCommandHandler(
    IDocVerificationChallengeRepository challengeRepository,
    IUserRepository userRepository,
    SocureSettings socureSettings,
    IValidator<ProcessWebhookCommand> validator,
    ILogger<ProcessWebhookCommandHandler> logger)
    : ICommandHandler<ProcessWebhookCommand>
{
    public async Task<Result> Handle(
        ProcessWebhookCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.Validate(command, cancellationToken);
        if (validationResult is ValidationFailedResult validationFailed)
        {
            logger.LogWarning("Webhook validation failed: {Errors}", validationFailed.Errors);
            return Result.ValidationFailed(validationFailed.Errors);
        }

        // Validate webhook signature (D11)
        if (!ValidateWebhookSignature(command.WebhookSignature))
        {
            logger.LogWarning("Webhook signature validation failed");
            return Result.Unauthorized("Invalid webhook signature.");
        }

        // Find the challenge by correlation keys (D6: ReferenceId primary, EvalId fallback)
        var challenge = await FindChallengeByCorrelation(
            command.ReferenceId, command.EvalId, cancellationToken);

        if (challenge == null)
        {
            logger.LogWarning(
                "Webhook received but no challenge found for ReferenceId={ReferenceId}, EvalId={EvalId}",
                command.ReferenceId, command.EvalId);
            // Return success to prevent Socure retries — challenge may have been cleaned up
            return Result.Success();
        }

        // Idempotency check (D8): if this event was already processed, return success
        if (challenge.SocureEventId == command.EventId)
        {
            logger.LogInformation(
                "Webhook event {EventId} already processed for challenge {ChallengeId}",
                command.EventId, challenge.PublicId);
            return Result.Success();
        }

        // Terminal state protection (D7): cannot modify a challenge that has already resolved
        if (challenge.IsTerminal)
        {
            logger.LogWarning(
                "Webhook event {EventId} arrived for terminal challenge {ChallengeId} (status: {Status})",
                command.EventId, challenge.PublicId, challenge.Status);
            return Result.Success();
        }

        // Determine the new status from the document decision
        var newStatus = MapDecisionToStatus(command.DocumentDecision);
        if (newStatus == null)
        {
            logger.LogWarning(
                "Webhook event {EventId} has unrecognized document decision: {Decision}",
                command.EventId, command.DocumentDecision);
            return Result.Success();
        }

        // Validate state transition (D7)
        if (challenge.Status != DocVerificationStatus.Pending)
        {
            logger.LogWarning(
                "Challenge {ChallengeId} is in {Status} state, cannot process webhook",
                challenge.PublicId, challenge.Status);
            return Result.Success();
        }

        // Apply the transition
        challenge.SocureEventId = command.EventId;
        challenge.TransitionTo(newStatus.Value);

        if (newStatus == DocVerificationStatus.Rejected)
        {
            challenge.OffboardingReason = "docVerificationFailed";
        }

        await challengeRepository.UpdateAsync(challenge, cancellationToken);

        logger.LogInformation(
            "Webhook event {EventId}: challenge {ChallengeId} transitioned to {Status}",
            command.EventId, challenge.PublicId, newStatus);

        // If verified: update user's proofing status and IAL level
        if (newStatus == DocVerificationStatus.Verified)
        {
            await UpdateUserProofingStatus(challenge.UserId, cancellationToken);
        }

        return Result.Success();
    }

    private bool ValidateWebhookSignature(string? signature)
    {
        // In dev/stub mode, skip signature validation (D11)
        if (socureSettings.UseStub)
        {
            return true;
        }

        // Placeholder: real validation will be implemented when Socure webhook signing is documented
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(socureSettings.WebhookSecret))
        {
            return false;
        }

        // TODO: Implement actual HMAC signature validation when Socure docs are available
        return true;
    }

    private async Task<DocVerificationChallenge?> FindChallengeByCorrelation(
        string? referenceId,
        string? evalId,
        CancellationToken cancellationToken)
    {
        // Primary lookup by ReferenceId (D6)
        if (!string.IsNullOrWhiteSpace(referenceId))
        {
            var challenge = await challengeRepository.GetBySocureReferenceIdAsync(
                referenceId, cancellationToken);
            if (challenge != null)
            {
                return challenge;
            }
        }

        // Fallback lookup by EvalId (D6)
        if (!string.IsNullOrWhiteSpace(evalId))
        {
            return await challengeRepository.GetByEvalIdAsync(evalId, cancellationToken);
        }

        return null;
    }

    private static DocVerificationStatus? MapDecisionToStatus(string? decision)
    {
        return decision?.ToLowerInvariant() switch
        {
            "accept" => DocVerificationStatus.Verified,
            "reject" => DocVerificationStatus.Rejected,
            _ => null
        };
    }

    private async Task UpdateUserProofingStatus(int userId, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetUserByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            logger.LogWarning("User {UserId} not found when updating proofing status after verification", userId);
            return;
        }

        user.IdProofingStatus = IdProofingStatus.Completed;
        user.IalLevel = UserIalLevel.IAL2;
        user.IdProofingCompletedAt = DateTime.UtcNow;

        await userRepository.UpdateUserAsync(user, cancellationToken);

        logger.LogInformation(
            "User {UserId} proofing status updated to Completed, IAL2 after document verification",
            userId);
    }
}
