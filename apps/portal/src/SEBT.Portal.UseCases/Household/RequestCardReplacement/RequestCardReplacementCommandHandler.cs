using Medallion.Threading;
using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Core.StateBackends;
using SEBT.Portal.Core.Utilities;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Handles card replacement requests for an authenticated user's household.
/// Validates input, resolves household identity, enforces minimum IAL, enforces
/// per-case self-service rules, enforces 2-week cooldown via portal DB, and
/// dispatches to the state backend. Persists replacement-request records for
/// future cooldown enforcement only when the backend reports success, so a
/// failed dispatch does not burn the user's 14-day cooldown for an action that
/// never executed. Backend policy rejections and backend errors are mapped to
/// portal <see cref="Result"/> types.
/// </summary>
public class RequestCardReplacementCommandHandler(
    IValidator<RequestCardReplacementCommand> validator,
    IHouseholdIdentifierResolver resolver,
    IHouseholdRepository repository,
    IIdProofingService idProofingService,
    ISelfServiceEvaluator selfServiceEvaluator,
    ICardReplacementBackend cardReplacementBackend,
    ICardReplacementRequestRepository cardReplacementRepo,
    IIdentifierHasher identifierHasher,
    ICooldownIdentityResolver cooldownIdentityResolver,
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
            logger.LogError(
                "Card replacement attempted but no household identifier could be resolved from claims");
            return Result.Unauthorized("Unable to identify user from token.");
        }

        var userIalLevel = UserIalLevelExtensions.FromClaimsPrincipal(command.User);

        var household = await repository.GetHouseholdByIdentifierAsync(
            identifier,
            new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false),
            userIalLevel,
            command.User.GetUserId(),
            cancellationToken: cancellationToken);

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
        var requestedSummerEbtCaseIds = command.CaseRefs
            .Select(r => r.SummerEbtCaseId)
            .ToHashSet(StringComparer.Ordinal);
        var requestedCases = household.SummerEbtCases
            .Where(c => c.SummerEBTCaseID != null && requestedSummerEbtCaseIds.Contains(c.SummerEBTCaseID))
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
            logger.LogError("Card replacement: unable to resolve user ID from claims");
            return Result.Unauthorized("Unable to identify user from token.");
        }

        var identifierKind = identifier.Type.ToString();

        // Distributed lock prevents TOCTOU race between cooldown check, backend
        // dispatch, and persist. Scoped to the user — a single user can only be
        // in one card replacement flow at a time. Note: held during the backend
        // call, which is acceptable because (a) the lock is per-user, not global,
        // and (b) the backend call has its own cancellation token plumbing.
        await using (await distributedLockProvider.AcquireLockAsync(
            $"CardReplacement:{userId.Value}", cancellationToken: cancellationToken))
        {
            // Check cooldown from portal DB — the authoritative source for request timestamps.
            var householdHash = identifierHasher.Hash(identifier.Value);
            var cooldownErrors = new List<ValidationError>();

            foreach (var caseRef in command.CaseRefs)
            {
                // Cooldown rows are keyed by the hash of the canonical (raw
                // state) case ID, never an encoding-specific token — hashing is
                // one-way, so an encoding change would silently reset cooldowns.
                var caseHash = identifierHasher.Hash(
                    cooldownIdentityResolver.ResolveCanonicalCaseIdentity(caseRef.SummerEbtCaseId));
                if (householdHash != null && caseHash != null)
                {
                    var hasCooldown = await cardReplacementRepo.HasRecentRequestAsync(
                        householdHash, caseHash, CooldownPeriod, cancellationToken);
                    if (hasCooldown)
                    {
                        cooldownErrors.Add(new ValidationError(
                            "CaseRefs",
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

            // Cooldown clear — dispatch to the state backend. Persist only on
            // backend success so a failed dispatch does not burn the 14-day
            // cooldown for a request that never executed.
            logger.LogInformation(
                "Card replacement dispatching to state backend for household identifier kind {Kind}, {Count} case(s)",
                identifierKind,
                command.CaseRefs.Count);

            // The command's case IDs are the opaque tokens the read path served;
            // the backend decodes them into whatever routing fields it needs.
            var backendRequest = new CardReplacementRequest(
                command.CaseRefs.Select(r => r.SummerEbtCaseId).ToList());

            WriteResult backendResult;
            try
            {
                backendResult = await cardReplacementBackend.RequestCardReplacementAsync(
                    backendRequest,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                // Backend threw before returning a result — treat as transient backend
                // failure. Cooldown is NOT recorded so the user can retry without
                // waiting 14 days.
                logger.LogError(
                    ex,
                    "Card replacement backend threw for household identifier kind {Kind}, {Count} case(s); cooldown NOT recorded, user may retry",
                    identifierKind,
                    command.CaseRefs.Count);
                return Result.DependencyFailed(
                    DependencyFailedReason.ConnectionFailed,
                    "Card replacement service is temporarily unavailable.");
            }

            if (!backendResult.IsSuccess)
            {
                if (backendResult.IsPolicyRejection)
                {
                    // State-side policy declined the request (e.g., card already in
                    // flight, case ineligible). Surface to the user as
                    // PreconditionFailed; do not cooldown so they can take a different
                    // action immediately.
                    logger.LogWarning(
                        "Card replacement policy rejection for household identifier kind {Kind}: {ErrorCode}; cooldown NOT recorded",
                        identifierKind,
                        backendResult.ErrorCode);
                    return Result.PreconditionFailed(
                        PreconditionFailedReason.Conflict,
                        backendResult.ErrorMessage);
                }

                logger.LogError(
                    "Card replacement backend error for household identifier kind {Kind}: {ErrorCode}; cooldown NOT recorded, user may retry",
                    identifierKind,
                    backendResult.ErrorCode);
                return Result.DependencyFailed(
                    DependencyFailedReason.ConnectionFailed,
                    backendResult.ErrorMessage);
            }

            // Backend reported success — persist replacement requests for cooldown
            // enforcement. If persistence fails after a successful dispatch, the
            // state-side write has executed but we have no portal-side record. Log
            // critically: the user is not blocked from re-requesting (state-side
            // dedup is the backstop until the next portal-side persist succeeds).
            try
            {
                foreach (var caseRef in command.CaseRefs)
                {
                    // Must resolve to the same canonical identity as the
                    // cooldown check above, or persisted rows would never match
                    // later lookups.
                    var caseHash = identifierHasher.Hash(
                        cooldownIdentityResolver.ResolveCanonicalCaseIdentity(caseRef.SummerEbtCaseId));
                    if (householdHash != null && caseHash != null)
                    {
                        await cardReplacementRepo.CreateAsync(
                            householdHash, caseHash, userId.Value, cancellationToken);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogCritical(
                    ex,
                    "Card replacement: backend reported success but cooldown persistence failed for household identifier kind {Kind}, {Count} case(s). Subsequent portal requests within {Days} days will not be cooldown-blocked; relying on state-side dedup.",
                    identifierKind,
                    command.CaseRefs.Count,
                    CooldownPeriod.TotalDays);
                // The user-facing action did execute — return success rather than
                // misleading the user with a failure for an action that happened.
                return Result.Success();
            }

            logger.LogInformation(
                "Card replacement request completed for household identifier kind {Kind}, {Count} case(s)",
                identifierKind,
                command.CaseRefs.Count);
            return Result.Success();
        }
    }
}
