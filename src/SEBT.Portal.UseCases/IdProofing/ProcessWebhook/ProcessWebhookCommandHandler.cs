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

        // Paused events are intermediate. No terminal decision yet, challenge stays Pending.
        if (string.Equals(command.EventType, "evaluation_paused", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation(
                "Webhook event {EventId}: evaluation_paused, challenge {ChallengeId} stays Pending",
                SanitizeForLogging(command.EventId), challenge.PublicId);
            return Result.Success();
        }

        // Route on the top-level workflow decision (DC-296). The DocV enrichment decision is
        // diagnostic only: it reflects document quality alone and can disagree with the
        // workflow outcome when Digital Intelligence signals drive a reject.
        var newStatus = MapWorkflowDecisionToStatus(command.WorkflowDecision);
        if (newStatus == null)
        {
            logger.LogWarning(
                "Webhook event {EventId} has unrecognized workflow decision: {WorkflowDecision} " +
                "(DocV enrichment decision was {DocumentDecision})",
                SanitizeForLogging(command.EventId),
                SanitizeForLogging(command.WorkflowDecision),
                SanitizeForLogging(command.DocumentDecision));
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
    /// Maps the top-level Socure workflow decision to our DocV challenge status (DC-296).
    /// ACCEPT -> Verified.
    /// REJECT -> Rejected.
    /// RESUBMIT -> Rejected. The workflow terminated (e.g. user declined on the Capture App
    /// before uploading). Treated as terminal rejection here; DC-138 resubmit scaffold will
    /// refine this once in-session retries are supported.
    /// REVIEW -> Rejected. DC does not use human review queues, so we treat review as a safe
    /// default reject.
    /// </summary>
    private static DocVerificationStatus? MapWorkflowDecisionToStatus(string? decision)
    {
        return decision?.ToUpperInvariant() switch
        {
            "ACCEPT" => DocVerificationStatus.Verified,
            "REJECT" => DocVerificationStatus.Rejected,
            "RESUBMIT" => DocVerificationStatus.Rejected,
            "REVIEW" => DocVerificationStatus.Rejected,
            _ => null
        };
    }

    private async Task UpdateUserProofingStatus(Guid userId, CancellationToken cancellationToken)
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
