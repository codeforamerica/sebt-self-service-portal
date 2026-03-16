using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Handles an incoming Socure webhook. Validates the signature (placeholder in dev),
/// checks idempotency via event_id, correlates to a challenge via ReferenceId/EvalId,
/// validates state transition, and updates both challenge and user state on verification.
/// </summary>
// TODO: Register webhook URLs in the Socure dashboard (https://help.socure.com/riskos/docs/webhooks)
// for each environment (sandbox, staging, production) before go-live.
public class ProcessWebhookCommandHandler(
    IDocVerificationChallengeRepository challengeRepository,
    IUserRepository userRepository,
    SocureSettings socureSettings,
    IValidator<ProcessWebhookCommand> validator,
    ILogger<ProcessWebhookCommandHandler> logger)
    : ICommandHandler<ProcessWebhookCommand>
{
    private static string SanitizeForLogging(string? value) =>
        value?.Replace("\r", string.Empty).Replace("\n", string.Empty) ?? string.Empty;

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

        // Validate webhook signature
        if (!ValidateWebhookSignature(command.WebhookSignature))
        {
            logger.LogWarning("Webhook signature validation failed");
            return Result.Unauthorized("Invalid webhook signature.");
        }

        // Find the challenge by correlation keys (ReferenceId primary, EvalId fallback)
        var challenge = await FindChallengeByCorrelation(
            command.ReferenceId, command.EvalId, cancellationToken);

        if (challenge == null)
        {
            // Log at Error — if this starts happening consistently it means the correlation
            // contract with Socure has changed and all webhooks are being silently dropped.
            logger.LogError(
                "Webhook received but no challenge found for ReferenceId={ReferenceId}, EvalId={EvalId}",
                SanitizeForLogging(command.ReferenceId), SanitizeForLogging(command.EvalId));
            // Return success to prevent Socure retries — challenge may have been cleaned up
            return Result.Success();
        }

        // Idempotency check: if this event was already processed, return success
        if (challenge.SocureEventId == command.EventId)
        {
            logger.LogInformation(
                "Webhook event {EventId} already processed for challenge {ChallengeId}",
                SanitizeForLogging(command.EventId), challenge.PublicId);
            return Result.Success();
        }

        // Terminal state protection: cannot modify a challenge that has already resolved
        if (challenge.IsTerminal)
        {
            logger.LogWarning(
                "Webhook event {EventId} arrived for terminal challenge {ChallengeId} (status: {Status})",
                SanitizeForLogging(command.EventId), challenge.PublicId, challenge.Status);
            return Result.Success();
        }

        // Determine the new status from the document decision
        if (IsIntermediateDecision(command.DocumentDecision))
        {
            logger.LogInformation(
                "Webhook event {EventId}: intermediate decision {Decision}, challenge {ChallengeId} stays Pending",
                SanitizeForLogging(command.EventId), SanitizeForLogging(command.DocumentDecision), challenge.PublicId);
            return Result.Success();
        }

        var newStatus = MapDecisionToStatus(command.DocumentDecision);
        if (newStatus == null)
        {
            logger.LogWarning(
                "Webhook event {EventId} has unrecognized document decision: {Decision}",
                SanitizeForLogging(command.EventId), SanitizeForLogging(command.DocumentDecision));
            return Result.Success();
        }

        // Validate state transition
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

        try
        {
            await challengeRepository.UpdateAsync(challenge, cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // Another thread already processed this challenge — our update lost the race.
            // Return success since the work was done by the winning thread.
            logger.LogInformation(
                "Webhook event {EventId}: concurrency conflict on challenge {ChallengeId}, " +
                "another thread already processed it",
                SanitizeForLogging(command.EventId), challenge.PublicId);
            return Result.Success();
        }

        logger.LogInformation(
            "Webhook event {EventId}: challenge {ChallengeId} transitioned to {Status}",
            SanitizeForLogging(command.EventId), challenge.PublicId, newStatus);

        // If verified: update user's proofing status and IAL level
        if (newStatus == DocVerificationStatus.Verified)
        {
            await UpdateUserProofingStatus(challenge.UserId, cancellationToken);
        }

        return Result.Success();
    }

    private bool ValidateWebhookSignature(string? bearerToken)
    {
        // In dev/stub mode, skip validation
        if (socureSettings.UseStub)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(bearerToken) || string.IsNullOrWhiteSpace(socureSettings.WebhookSecret))
        {
            return false;
        }

        // Constant-time comparison to prevent timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(bearerToken),
            Encoding.UTF8.GetBytes(socureSettings.WebhookSecret));
    }

    private async Task<DocVerificationChallenge?> FindChallengeByCorrelation(
        string? referenceId,
        string? evalId,
        CancellationToken cancellationToken)
    {
        // Primary lookup by ReferenceId
        if (!string.IsNullOrWhiteSpace(referenceId))
        {
            var challenge = await challengeRepository.GetBySocureReferenceIdAsync(
                referenceId, cancellationToken);
            if (challenge != null)
            {
                return challenge;
            }
        }

        // Fallback lookup by EvalId
        if (!string.IsNullOrWhiteSpace(evalId))
        {
            return await challengeRepository.GetByEvalIdAsync(evalId, cancellationToken);
        }

        return null;
    }

    /// <summary>
    /// Returns true for Socure decisions that mean "not done yet" — the challenge stays Pending.
    /// review: escalated to manual human review, follow-up webhook will arrive.
    /// resubmit: document quality insufficient, user can retry within the existing session.
    /// </summary>
    private static bool IsIntermediateDecision(string? decision)
    {
        return decision?.ToLowerInvariant() is "review" or "resubmit";
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
