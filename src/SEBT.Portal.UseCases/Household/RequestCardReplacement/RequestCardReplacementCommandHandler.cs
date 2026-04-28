using Medallion.Threading;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Handles card replacement requests for an authenticated user's household.
/// Validates input, resolves household identity, enforces minimum IAL,
/// enforces self-service rules, enforces 2-week cooldown via portal DB,
/// and persists replacement requests for future cooldown enforcement.
/// State connector call is stubbed — actual card replacement is a future integration.
/// </summary>
public class RequestCardReplacementCommandHandler(
    IValidator<RequestCardReplacementCommand> validator,
    IHouseholdIdentifierResolver resolver,
    IHouseholdRepository repository,
    IIdProofingService idProofingService,
    ISelfServiceEvaluator selfServiceEvaluator,
    ICardReplacementRequestRepository cardReplacementRepo,
    IIdentifierHasher identifierHasher,
    IDistributedLockProvider distributedLockProvider,
    ILogger<RequestCardReplacementCommandHandler> logger)
    : ICommandHandler<RequestCardReplacementCommand>
{
    private static readonly TimeSpan CooldownPeriod = TimeSpan.FromDays(14);

    public async Task<Result> Handle(
        RequestCardReplacementCommand command,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.Validate(command, cancellationToken);
        if (validationResult is ValidationFailedResult validationFailed)
        {
            logger.LogWarning("Card replacement validation failed");
            return Result.ValidationFailed(validationFailed.Errors);
        }

        var identifier = await resolver.ResolveAsync(command.User, cancellationToken);
        if (identifier == null)
        {
            logger.LogWarning(
                "Card replacement attempted but no household identifier could be resolved from claims");
            return Result.Unauthorized("Unable to identify user from token.");
        }

        var userIalLevel = UserIalLevelExtensions.FromClaimsPrincipal(command.User);

        var household = await repository.GetHouseholdByIdentifierAsync(
            identifier,
            new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false),
            userIalLevel,
            cancellationToken);

        if (household == null)
        {
            logger.LogWarning("Card replacement attempted but household data not found");
            return Result.PreconditionFailed(PreconditionFailedReason.NotFound, "Household data not found.");
        }

        // SECURITY: Block write operations when the user has not met the IAL
        // required by their cases. See docs/config/ial/README.md.
        var decision = idProofingService.Evaluate(
            ProtectedResource.Card, ProtectedAction.Write,
            userIalLevel, household.SummerEbtCases);
        if (!decision.IsAllowed)
        {
            logger.LogInformation(
                "Card replacement denied: user IAL {UserIal} is below required {RequiredIal}",
                userIalLevel,
                decision.RequiredLevel);
            return Result.Forbidden(
                $"This household requires {decision.RequiredLevel}. Complete identity verification to request card replacements.");
        }

        // Co-loaded cases are managed by caseworkers, not the portal.
        var requestedCases = household.SummerEbtCases
            .Where(c => c.SummerEBTCaseID != null && command.CaseIds.Contains(c.SummerEBTCaseID))
            .ToList();
        if (requestedCases.Any(c => c.IsCoLoaded))
        {
            logger.LogWarning(
                "Card replacement rejected: request includes co-loaded case(s)");
            return Result.PreconditionFailed(
                PreconditionFailedReason.Conflict,
                "Card replacements are not available for co-loaded benefits. Please contact your case worker.");
        }

        // Per-case self-service rules enforcement: each case's own issuance type
        // and card status determine eligibility (per James's 4.3.26 guidance that
        // self-service actions are case-scoped, not household-scoped).
        foreach (var summerEbtCase in requestedCases)
        {
            var allowedActions = selfServiceEvaluator.Evaluate(summerEbtCase);
            if (!allowedActions.CanRequestReplacementCard)
            {
                logger.LogInformation("Card replacement denied by self-service rules for case");
                return Result.PreconditionFailed(
                    PreconditionFailedReason.NotAllowed,
                    allowedActions.CardReplacementDeniedMessageKey ?? "Card replacement is not available for this account.");
            }
        }

        // Resolve the user's database ID early — needed for lock key and audit trail FK.
        var userId = command.User.GetUserId();
        if (userId == null)
        {
            logger.LogWarning("Card replacement: unable to resolve user ID from claims");
            return Result.Unauthorized("Unable to identify user from token.");
        }

        // Distributed lock prevents TOCTOU race between cooldown check and persist.
        // Scoped to the user — a single user can only be in one card replacement flow at a time.
        await using (await distributedLockProvider.AcquireLockAsync(
            $"CardReplacement:{userId.Value}", cancellationToken: cancellationToken))
        {
            // Check cooldown from portal DB — the authoritative source for request timestamps.
            var householdHash = identifierHasher.Hash(identifier.Value);
            var cooldownErrors = new List<ValidationError>();

            foreach (var caseId in command.CaseIds)
            {
                var caseHash = identifierHasher.Hash(caseId);
                if (householdHash != null && caseHash != null)
                {
                    var hasCooldown = await cardReplacementRepo.HasRecentRequestAsync(
                        householdHash, caseHash, CooldownPeriod, cancellationToken);
                    if (hasCooldown)
                    {
                        cooldownErrors.Add(new ValidationError(
                            "CaseIds",
                            $"A card replacement was requested for this case within the last 14 days."));
                    }
                }
            }

            if (cooldownErrors.Count > 0)
            {
                logger.LogInformation(
                    "Card replacement rejected: {Count} case(s) within cooldown period",
                    cooldownErrors.Count);
                return Result.ValidationFailed(cooldownErrors);
            }

            // Persist replacement requests to portal DB for cooldown enforcement
            foreach (var caseId in command.CaseIds)
            {
                var caseHash = identifierHasher.Hash(caseId);
                if (householdHash != null && caseHash != null)
                {
                    await cardReplacementRepo.CreateAsync(
                        householdHash, caseHash, userId.Value, cancellationToken);
                }
            }
        }

        var identifierKind = identifier.Type.ToString();
        logger.LogInformation(
            "Card replacement request recorded for household identifier kind {Kind}, {Count} case(s)",
            identifierKind,
            command.CaseIds.Count);

        return Result.Success();
    }
}
