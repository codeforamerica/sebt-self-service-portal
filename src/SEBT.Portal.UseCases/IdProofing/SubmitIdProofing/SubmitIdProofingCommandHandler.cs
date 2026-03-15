using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.IdProofing;

/// <summary>
/// Handles ID proofing submission. Orchestrates the flow:
/// 1. Validate input
/// 2. Early exit if no ID provided (noIdProvided off-boarding)
/// 3. Reuse existing active challenge if one exists
/// 4. Call Socure for risk assessment
/// 5. Create a new challenge if document verification is required
/// </summary>
public class SubmitIdProofingCommandHandler(
    IUserRepository userRepository,
    IDocVerificationChallengeRepository challengeRepository,
    ISocureClient socureClient,
    SocureSettings socureSettings,
    IValidator<SubmitIdProofingCommand> validator,
    ILogger<SubmitIdProofingCommandHandler> logger)
    : ICommandHandler<SubmitIdProofingCommand, SubmitIdProofingResponse>
{
    public async Task<Result<SubmitIdProofingResponse>> Handle(
        SubmitIdProofingCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.Validate(command, cancellationToken);
        if (validationResult is ValidationFailedResult validationFailed)
        {
            logger.LogWarning("ID proofing submission validation failed for user {UserId}", command.UserId);
            return Result<SubmitIdProofingResponse>.ValidationFailed(validationFailed.Errors);
        }

        // Load the user — needed for email (passed to Socure)
        var user = await userRepository.GetUserByIdAsync(command.UserId, cancellationToken);
        if (user == null)
        {
            logger.LogWarning("User {UserId} not found for ID proofing submission", command.UserId);
            return Result<SubmitIdProofingResponse>.PreconditionFailed(
                PreconditionFailedReason.NotFound, "User not found.");
        }

        // No ID provided → off-board immediately (Codex test 5)
        if (string.IsNullOrWhiteSpace(command.IdType))
        {
            logger.LogInformation("User {UserId} submitted ID proofing without an ID type", command.UserId);
            return Result<SubmitIdProofingResponse>.Success(
                new SubmitIdProofingResponse("failed", OffboardingReason: "noIdProvided"));
        }

        // Check for an existing active challenge → reuse instead of creating a duplicate
        var activeChallenge = await challengeRepository.GetActiveByUserIdAsync(
            command.UserId, cancellationToken);
        if (activeChallenge != null)
        {
            logger.LogInformation(
                "Reusing existing active challenge {ChallengeId} for user {UserId}",
                activeChallenge.PublicId, command.UserId);
            return Result<SubmitIdProofingResponse>.Success(
                new SubmitIdProofingResponse(
                    "documentVerificationRequired",
                    ChallengeId: activeChallenge.PublicId,
                    AllowIdRetry: activeChallenge.AllowIdRetry));
        }

        // Call Socure for risk assessment
        var assessmentResult = await socureClient.RunIdProofingAssessmentAsync(
            command.UserId,
            user.Email,
            command.DateOfBirth,
            command.IdType,
            command.IdValue,
            cancellationToken);

        if (!assessmentResult.IsSuccess)
        {
            logger.LogWarning("Socure assessment failed for user {UserId}", command.UserId);

            if (assessmentResult is DependencyFailedResult<IdProofingAssessmentResult> depFailed)
            {
                return Result<SubmitIdProofingResponse>.DependencyFailed(
                    depFailed.Reason, depFailed.Message);
            }

            return Result<SubmitIdProofingResponse>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed, "Socure risk assessment failed.");
        }

        var assessment = assessmentResult.Value;

        return assessment.Outcome switch
        {
            IdProofingOutcome.Matched => Result<SubmitIdProofingResponse>.Success(
                new SubmitIdProofingResponse("matched")),

            IdProofingOutcome.Failed => Result<SubmitIdProofingResponse>.Success(
                new SubmitIdProofingResponse(
                    "failed",
                    AllowIdRetry: assessment.AllowIdRetry,
                    OffboardingReason: "idProofingFailed")),

            IdProofingOutcome.DocumentVerificationRequired =>
                await CreateChallengeAndRespond(command.UserId, assessment, cancellationToken),

            _ => throw new InvalidOperationException(
                $"Unexpected IdProofingOutcome: {assessment.Outcome}")
        };
    }

    private async Task<Result<SubmitIdProofingResponse>> CreateChallengeAndRespond(
        int userId,
        IdProofingAssessmentResult assessment,
        CancellationToken cancellationToken)
    {
        var challenge = new DocVerificationChallenge
        {
            UserId = userId,
            AllowIdRetry = assessment.AllowIdRetry,
            ExpiresAt = DateTime.UtcNow.AddMinutes(socureSettings.ChallengeExpirationMinutes),
            DocvTransactionToken = assessment.DocvSession?.DocvTransactionToken,
            DocvUrl = assessment.DocvSession?.DocvUrl,
            SocureReferenceId = assessment.DocvSession?.ReferenceId,
            EvalId = assessment.DocvSession?.EvalId
        };

        await challengeRepository.CreateAsync(challenge, cancellationToken);

        logger.LogInformation(
            "Created doc verification challenge {ChallengeId} for user {UserId}",
            challenge.PublicId, userId);

        return Result<SubmitIdProofingResponse>.Success(
            new SubmitIdProofingResponse(
                "documentVerificationRequired",
                ChallengeId: challenge.PublicId,
                AllowIdRetry: assessment.AllowIdRetry));
    }
}
