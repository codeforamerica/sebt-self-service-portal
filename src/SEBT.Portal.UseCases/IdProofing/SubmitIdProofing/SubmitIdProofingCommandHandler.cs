using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
using SEBT.Portal.Core.Exceptions;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.DocVerification;
using SEBT.Portal.Core.Models.Household;
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
    IHouseholdRepository householdRepository,
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

        // Max attempts reached → off-board (3-attempt cap)
        const int maxAttempts = 3;
        if (user.IdProofingAttemptCount >= maxAttempts)
        {
            logger.LogInformation(
                "User {UserId} has reached the maximum ID proofing attempts ({MaxAttempts})",
                command.UserId, maxAttempts);
            return Result<SubmitIdProofingResponse>.Success(
                new SubmitIdProofingResponse("failed",
                    AllowIdRetry: false,
                    OffboardingReason: "maxAttemptsReached"));
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

        // Fetch household data for user's name and address (best-effort, optional for Socure)
        string? givenName = null;
        string? familyName = null;
        Address? address = null;
        try
        {
            var household = await householdRepository.GetHouseholdByEmailAsync(
                user.Email,
                new PiiVisibility(IncludeAddress: true, IncludeEmail: false, IncludePhone: false),
                user.IalLevel,
                cancellationToken);
            if (household?.UserProfile != null)
            {
                givenName = household.UserProfile.FirstName;
                familyName = household.UserProfile.LastName;
            }
            if (household?.AddressOnFile != null)
            {
                address = household.AddressOnFile;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Household lookup failed for user {UserId}, proceeding without name/address",
                command.UserId);
        }

        // Call Socure for risk assessment
        // Sandbox phone override lets developers receive DocV SMS on a real phone
        // without storing personal numbers in the database.
        var phoneNumber = !string.IsNullOrWhiteSpace(socureSettings.SandboxPhoneOverride)
            ? socureSettings.SandboxPhoneOverride
            : user.Phone;

        var assessmentResult = await socureClient.RunIdProofingAssessmentAsync(
            command.UserId,
            user.Email,
            command.DateOfBirth,
            command.IdType,
            command.IdValue,
            ipAddress: command.IpAddress,
            phoneNumber: phoneNumber,
            givenName: givenName,
            familyName: familyName,
            address: address,
            diSessionToken: command.DiSessionToken,
            cancellationToken: cancellationToken);

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

        // Increment attempt count (persisted below with each outcome's save)
        user.IdProofingAttemptCount++;

        // Derive retry eligibility from attempt count (overrides Socure's value)
        var allowIdRetry = user.IdProofingAttemptCount < maxAttempts;

        switch (assessment.Outcome)
        {
            case IdProofingOutcome.Matched:
                // Single save: attempt count + proofing completion together
                return await CompleteProofingAndRespond(user, cancellationToken);

            case IdProofingOutcome.Failed:
                await userRepository.UpdateUserAsync(user, cancellationToken);
                return Result<SubmitIdProofingResponse>.Success(
                    new SubmitIdProofingResponse(
                        "failed",
                        AllowIdRetry: allowIdRetry,
                        OffboardingReason: "idProofingFailed"));

            case IdProofingOutcome.DocumentVerificationRequired:
                await userRepository.UpdateUserAsync(user, cancellationToken);
                return await CreateChallengeAndRespond(
                    command.UserId, assessment, allowIdRetry, cancellationToken);

            default:
                throw new InvalidOperationException(
                    $"Unexpected IdProofingOutcome: {assessment.Outcome}");
        }
    }

    private async Task<Result<SubmitIdProofingResponse>> CompleteProofingAndRespond(
        User user,
        CancellationToken cancellationToken)
    {
        user.IdProofingStatus = IdProofingStatus.Completed;
        user.IalLevel = UserIalLevel.IAL2;
        user.IdProofingCompletedAt = DateTime.UtcNow;
        await userRepository.UpdateUserAsync(user, cancellationToken);

        logger.LogInformation(
            "User {UserId} proofing completed via Socure ACCEPT (no DocV required)",
            user.Id);

        return Result<SubmitIdProofingResponse>.Success(
            new SubmitIdProofingResponse("matched"));
    }

    private async Task<Result<SubmitIdProofingResponse>> CreateChallengeAndRespond(
        int userId,
        IdProofingAssessmentResult assessment,
        bool allowIdRetry,
        CancellationToken cancellationToken)
    {
        var challenge = new DocVerificationChallenge
        {
            UserId = userId,
            AllowIdRetry = allowIdRetry,
            ExpiresAt = DateTime.UtcNow.AddMinutes(socureSettings.ChallengeExpirationMinutes),
            DocvTransactionToken = assessment.DocvSession?.DocvTransactionToken,
            DocvUrl = assessment.DocvSession?.DocvUrl,
            SocureReferenceId = assessment.DocvSession?.ReferenceId,
            EvalId = assessment.DocvSession?.EvalId
        };

        try
        {
            await challengeRepository.CreateAsync(challenge, cancellationToken);
        }
        catch (DuplicateRecordException)
        {
            // Race condition: another request inserted a challenge between our check and insert.
            // Re-query and reuse the winner's challenge.
            logger.LogWarning(
                "Duplicate active challenge detected for user {UserId}, reusing existing challenge",
                userId);

            var existing = await challengeRepository.GetActiveByUserIdAsync(userId, cancellationToken);
            if (existing == null)
            {
                throw;
            }

            return Result<SubmitIdProofingResponse>.Success(
                new SubmitIdProofingResponse(
                    "documentVerificationRequired",
                    ChallengeId: existing.PublicId,
                    AllowIdRetry: existing.AllowIdRetry));
        }

        logger.LogInformation(
            "Created doc verification challenge {ChallengeId} for user {UserId}",
            challenge.PublicId, userId);

        return Result<SubmitIdProofingResponse>.Success(
            new SubmitIdProofingResponse(
                "documentVerificationRequired",
                ChallengeId: challenge.PublicId,
                AllowIdRetry: allowIdRetry));
    }
}
