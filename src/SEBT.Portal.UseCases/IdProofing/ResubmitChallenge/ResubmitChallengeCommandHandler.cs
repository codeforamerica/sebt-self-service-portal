using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.AppSettings;
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
/// Handles a user-initiated retry after a Socure RESUBMIT decision (DC-301).
///
/// RESUBMIT is terminal at Socure — the original workflow ended and cannot be resumed.
/// This handler starts a brand-new evaluation against the <c>docv_stepup</c> workflow
/// (which only emits ACCEPT/REJECT, structurally capping retries at one) and persists a
/// fresh <c>DocVerificationChallenge</c> row in Pending. The prior Resubmit challenge is
/// left untouched as a terminal record.
/// </summary>
public class ResubmitChallengeCommandHandler(
    IDocVerificationChallengeRepository challengeRepository,
    IUserRepository userRepository,
    IHouseholdRepository householdRepository,
    ISocureClient socureClient,
    SocureSettings socureSettings,
    IValidator<ResubmitChallengeCommand> validator,
    ILogger<ResubmitChallengeCommandHandler> logger)
    : ICommandHandler<ResubmitChallengeCommand, ResubmitChallengeResponse>
{
    public async Task<Result<ResubmitChallengeResponse>> Handle(
        ResubmitChallengeCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.Validate(command, cancellationToken);
        if (validationResult is ValidationFailedResult validationFailed)
        {
            logger.LogWarning("ResubmitChallenge validation failed for user {UserId}", command.UserId);
            return Result<ResubmitChallengeResponse>.ValidationFailed(validationFailed.Errors);
        }

        // IDOR-safe load: returns null when (publicId, userId) doesn't match
        var priorChallenge = await challengeRepository.GetByPublicIdAsync(
            command.ChallengeId, command.UserId, cancellationToken);
        if (priorChallenge == null)
        {
            logger.LogWarning(
                "ResubmitChallenge: challenge {ChallengeId} not found for user {UserId}",
                command.ChallengeId, command.UserId);
            return Result<ResubmitChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.NotFound, "Challenge not found.");
        }

        if (priorChallenge.Status != DocVerificationStatus.Resubmit)
        {
            logger.LogWarning(
                "ResubmitChallenge: challenge {ChallengeId} is in {Status} state, expected Resubmit",
                command.ChallengeId, priorChallenge.Status);
            return Result<ResubmitChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.Conflict,
                $"Challenge is in {priorChallenge.Status} state and cannot be resubmitted.");
        }

        var user = await userRepository.GetUserByIdAsync(command.UserId, cancellationToken);
        if (user == null)
        {
            logger.LogWarning("ResubmitChallenge: user {UserId} not found", command.UserId);
            return Result<ResubmitChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.NotFound, "User not found.");
        }

        if (string.IsNullOrWhiteSpace(user.Email))
        {
            logger.LogWarning(
                "ResubmitChallenge: user {UserId} has no email, cannot retry document verification",
                command.UserId);
            return Result<ResubmitChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.Conflict,
                "Email is required for document verification.");
        }

        // Best-effort household lookup, mirroring StartChallenge's refresh path. Socure's docv_stepup
        // accepts an empty payload (sandbox-confirmed), but we pass name/address/phone when we have
        // them so DI signals stay consistent with the original consumer_onboarding evaluation.
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
            logger.LogWarning(
                "ResubmitChallenge: household lookup failed ({ExceptionType}) for user {UserId}, proceeding without name/address/phone from CMS",
                ex.GetType().FullName, command.UserId);
        }

        var phoneNumber = !string.IsNullOrWhiteSpace(socureSettings.SandboxPhoneOverride)
            ? socureSettings.SandboxPhoneOverride
            : !string.IsNullOrWhiteSpace(householdPhone)
                ? householdPhone
                : user.Phone;

        var assessmentResult = await socureClient.RunDocvStepupAssessmentAsync(
            command.UserId,
            user.Email!,
            phoneNumber: phoneNumber,
            givenName: givenName,
            familyName: familyName,
            address: address,
            diSessionToken: null,
            cancellationToken: cancellationToken);

        if (!assessmentResult.IsSuccess)
        {
            logger.LogWarning(
                "ResubmitChallenge: docv_stepup assessment failed for user {UserId}",
                command.UserId);
            if (assessmentResult is DependencyFailedResult<IdProofingAssessmentResult> depFailed)
            {
                return Result<ResubmitChallengeResponse>.DependencyFailed(depFailed.Reason, depFailed.Message);
            }
            return Result<ResubmitChallengeResponse>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "Failed to start document verification retry.");
        }

        var assessment = assessmentResult.Value;
        if (assessment.Outcome != IdProofingOutcome.DocumentVerificationRequired
            || assessment.DocvSession == null)
        {
            logger.LogWarning(
                "ResubmitChallenge: docv_stepup returned unexpected outcome {Outcome} (DocvSession={HasSession}) for user {UserId}",
                assessment.Outcome, assessment.DocvSession != null, command.UserId);
            return Result<ResubmitChallengeResponse>.PreconditionFailed(
                PreconditionFailedReason.Conflict,
                "Unable to start document verification retry. Please try again later.");
        }

        var session = assessment.DocvSession;
        var newChallenge = new DocVerificationChallenge
        {
            UserId = command.UserId,
            SocureReferenceId = session.ReferenceId,
            EvalId = session.EvalId,
            DocvTransactionToken = session.DocvTransactionToken,
            DocvUrl = session.DocvUrl,
            DocvTokenIssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(socureSettings.ChallengeExpirationMinutes),
            AllowIdRetry = true
        };
        newChallenge.TransitionTo(DocVerificationStatus.Pending);

        await challengeRepository.CreateAsync(newChallenge, cancellationToken);

        logger.LogInformation(
            "ResubmitChallenge: opened fresh challenge {NewChallengeId} for user {UserId} (prior {PriorChallengeId} stays Resubmit)",
            newChallenge.PublicId, command.UserId, priorChallenge.PublicId);

        return Result<ResubmitChallengeResponse>.Success(
            new ResubmitChallengeResponse(
                ChallengeId: newChallenge.PublicId,
                DocvTransactionToken: session.DocvTransactionToken,
                DocvUrl: session.DocvUrl));
    }
}
