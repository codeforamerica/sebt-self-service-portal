using System.Globalization;
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
/// 4. Co-loaded users with SNAP/TANF ID: complete at IAL1+ without Socure (DC warehouse IC+DOB when applicable, then on-file match)
/// 5. Load household PII for Socure when available (name, address, phone from state/CMS)
/// 6. Call Socure for risk assessment
/// 7. Create a new challenge if document verification is required
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

        if (!DateOnly.TryParse(
                command.DateOfBirth,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var submittedDob))
        {
            logger.LogWarning(
                "ID proofing submission rejected for user {UserId}: DateOfBirth could not be parsed as yyyy-MM-dd",
                command.UserId);
            return Result<SubmitIdProofingResponse>.ValidationFailed(
                nameof(SubmitIdProofingCommand.DateOfBirth),
                "DateOfBirth must be a valid date in yyyy-MM-dd format.");
        }

        // Defense-in-depth: SSN/ITIN must be exactly 9 digits after stripping non-digit
        // characters. OpenAPI permits 4-digit partial SSNs, but product decision for DC-296
        // is full 9 only (aligned with the frontend schema). Other id types have different
        // format rules and are not policed here.
        if (IsSsnOrItin(command.IdType)
            && !IsExactlyNineDigitsAfterStripping(command.IdValue))
        {
            logger.LogWarning(
                "ID proofing submission rejected for user {UserId}: IdValue for SSN/ITIN is not 9 digits",
                command.UserId);
            return Result<SubmitIdProofingResponse>.ValidationFailed(
                nameof(SubmitIdProofingCommand.IdValue),
                "IdValue must be 9 digits for SSN or ITIN.");
        }

        // Load the user — needed for email (passed to Socure)
        var user = await userRepository.GetUserByIdAsync(command.UserId, cancellationToken);
        if (user == null)
        {
            logger.LogWarning("User {UserId} not found for ID proofing submission", command.UserId);
            return Result<SubmitIdProofingResponse>.PreconditionFailed(
                PreconditionFailedReason.NotFound, "User not found.");
        }

        // Socure requires an email address. OIDC users who reach this point without
        // an email cannot proceed with ID proofing.
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            logger.LogWarning("User {UserId} has no email, cannot submit ID proofing", command.UserId);
            return Result<SubmitIdProofingResponse>.PreconditionFailed(
                PreconditionFailedReason.Conflict, "Email is required for ID proofing.");
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

        // Co-loaded users still need a SNAP/TANF identifier so we can household them; off-board
        // when no ID is provided. Non-co-loaded users fall through to Socure DocV — Socure's
        // consumer_onboarding workflow short-circuits to document verification when KYC can't
        // resolve the consumer, so national_id is optional for that path.
        if (string.IsNullOrWhiteSpace(command.IdType))
        {
            if (user.IsCoLoaded)
            {
                logger.LogInformation(
                    "Co-loaded user {UserId} submitted ID proofing without an ID type; off-boarding (householding requires a benefit ID)",
                    command.UserId);
                return Result<SubmitIdProofingResponse>.Success(
                    new SubmitIdProofingResponse("failed", OffboardingReason: "noIdProvided"));
            }

            logger.LogInformation(
                "Non-co-loaded user {UserId} submitted ID proofing without an ID type; proceeding to Socure DocV with no national_id",
                command.UserId);
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

        // Persist the parsed DOB on the user; all downstream save paths will carry it through.
        user.DateOfBirth = submittedDob;

        // Co-loaded discovery: SNAP/TANF ids are an in-portal lookup (never Socure as national_id).
        // User-level IsCoLoaded isn't presumed from a pre-populated flag — the match itself is the
        // determination, and on success we persist it so downstream UI flows can rely on the claim.
        if (IdProofingBenefitIdentifierTypes.IsSnapOrTanfPortalSelection(command.IdType)
            && !string.IsNullOrWhiteSpace(command.IdValue))
        {
            try
            {
                if (await householdRepository.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync(
                        command.IdValue.Trim(),
                        submittedDob,
                        cancellationToken))
                {
                    logger.LogInformation(
                        "User {UserId} co-loaded benefit ID verified via DC warehouse (IC+DOB) for type {IdType}",
                        command.UserId,
                        command.IdType);
                    return await CompleteProofingAndRespond(
                        user,
                        UserIalLevel.IAL1plus,
                        cancellationToken,
                        "co-loaded SNAP/TANF matched via DC GetHouseholdByGuardian IC+DOB (no Socure)",
                        markCoLoaded: true);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    "DC warehouse IC+DOB match failed ({ExceptionType}) for co-loaded benefit ID verification for user {UserId}",
                    ex.GetType().Name,
                    command.UserId);
            }

            HouseholdData? benefitHousehold = null;
            try
            {
                benefitHousehold = await householdRepository.GetHouseholdByEmailAsync(
                    user.Email,
                    new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false),
                    user.IalLevel,
                    cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    "Household lookup failed ({ExceptionType}) for co-loaded benefit ID verification for user {UserId}",
                    ex.GetType().Name,
                    command.UserId);
            }

            if (CoLoadedBenefitIdentifierMatch.Matches(user, benefitHousehold, command.IdType, command.IdValue))
            {
                logger.LogInformation(
                    "User {UserId} co-loaded benefit ID verified for type {IdType}",
                    command.UserId, command.IdType);
                return await CompleteProofingAndRespond(
                    user,
                    UserIalLevel.IAL1plus,
                    cancellationToken,
                    "co-loaded SNAP/TANF matched to on-file records (no Socure)",
                    markCoLoaded: true);
            }

            logger.LogInformation(
                "User {UserId} co-loaded benefit ID did not match on-file records for type {IdType}",
                command.UserId, command.IdType);

            user.IdProofingAttemptCount++;
            var allowBenefitRetry = user.IdProofingAttemptCount < maxAttempts;
            await userRepository.UpdateUserAsync(user, cancellationToken);
            return Result<SubmitIdProofingResponse>.Success(
                new SubmitIdProofingResponse(
                    "failed",
                    AllowIdRetry: allowBenefitRetry,
                    OffboardingReason: "idProofingFailed"));
        }

        // Fetch household data for Socure: state/CMS may supply name, address, and phone when available.
        string? givenName = null;
        string? familyName = null;
        Address? address = null;
        string? householdPhone = null;
        try
        {
            var household = await householdRepository.GetHouseholdByEmailAsync(
                user.Email,
                new PiiVisibility(IncludeAddress: true, IncludeEmail: true, IncludePhone: true),
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
            householdPhone = household?.Phone;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Household lookup failed for user {UserId}, proceeding without name/address/phone from CMS",
                command.UserId);
        }

        // Sandbox phone override lets developers receive DocV SMS on a real phone
        // without storing personal numbers in the database.
        var phoneNumber = !string.IsNullOrWhiteSpace(socureSettings.SandboxPhoneOverride)
            ? socureSettings.SandboxPhoneOverride
            : !string.IsNullOrWhiteSpace(householdPhone)
                ? householdPhone
                : user.Phone;

        // Call Socure for risk assessment
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
                return await CompleteProofingAndRespond(
                    user,
                    UserIalLevel.IAL2,
                    cancellationToken,
                    "Socure ACCEPT (no DocV required)");

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
                    command, assessment, allowIdRetry, cancellationToken);

            default:
                throw new InvalidOperationException(
                    $"Unexpected IdProofingOutcome: {assessment.Outcome}");
        }
    }

    private async Task<Result<SubmitIdProofingResponse>> CompleteProofingAndRespond(
        User user,
        UserIalLevel ialLevelOnCompletion,
        CancellationToken cancellationToken,
        string completionReasonForLog,
        bool markCoLoaded = false)
    {
        user.IdProofingStatus = IdProofingStatus.Completed;
        user.IalLevel = ialLevelOnCompletion;
        user.IdProofingCompletedAt = DateTime.UtcNow;

        if (markCoLoaded)
        {
            user.IsCoLoaded = true;
            user.CoLoadedLastUpdated = DateTime.UtcNow;
        }

        await userRepository.UpdateUserAsync(user, cancellationToken);

        logger.LogInformation(
            "User {UserId} ID proofing completed: {Reason}",
            user.Id,
            completionReasonForLog);

        return Result<SubmitIdProofingResponse>.Success(
            new SubmitIdProofingResponse("matched"));
    }

    private async Task<Result<SubmitIdProofingResponse>> CreateChallengeAndRespond(
        SubmitIdProofingCommand command,
        IdProofingAssessmentResult assessment,
        bool allowIdRetry,
        CancellationToken cancellationToken)
    {
        var userId = command.UserId;
        var challenge = new DocVerificationChallenge
        {
            UserId = userId,
            AllowIdRetry = allowIdRetry,
            ExpiresAt = DateTime.UtcNow.AddMinutes(socureSettings.ChallengeExpirationMinutes),
            DocvTransactionToken = assessment.DocvSession?.DocvTransactionToken,
            DocvUrl = assessment.DocvSession?.DocvUrl,
            SocureReferenceId = assessment.DocvSession?.ReferenceId,
            EvalId = assessment.DocvSession?.EvalId,
            ProofingDateOfBirth = command.DateOfBirth,
            ProofingIdType = command.IdType,
            ProofingIdValue = command.IdValue,
            DocvTokenIssuedAt = assessment.DocvSession != null ? DateTime.UtcNow : null
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

    private static bool IsSsnOrItin(string? idType)
    {
        return string.Equals(idType, "ssn", StringComparison.OrdinalIgnoreCase)
            || string.Equals(idType, "itin", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExactlyNineDigitsAfterStripping(string? idValue)
    {
        if (idValue is null)
        {
            return false;
        }

        var digitCount = 0;
        foreach (var ch in idValue)
        {
            if (char.IsDigit(ch))
            {
                digitCount++;
                if (digitCount > 9)
                {
                    return false;
                }
            }
        }
        return digitCount == 9;
    }
}
