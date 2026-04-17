using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using IStateCardReplacementService = SEBT.Portal.StatesPlugins.Interfaces.ICardReplacementService;
using PluginCardReplacementRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.CardReplacementRequest;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Handles card replacement requests for an authenticated user's household.
/// Validates input, resolves household identity, enforces 2-week cooldown, and
/// dispatches to the state connector. Policy rejections and backend errors from
/// the connector are mapped to portal <see cref="Result"/> types.
/// </summary>
public class RequestCardReplacementCommandHandler(
    IValidator<RequestCardReplacementCommand> validator,
    IHouseholdIdentifierResolver resolver,
    IHouseholdRepository repository,
    IMinimumIalService minimumIalService,
    IStateCardReplacementService cardReplacementService,
    TimeProvider timeProvider,
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

        // SECURITY: Block write operations when the user has not met the minimum IAL
        // required by their cases. See docs/tdd/minimum-ial-determination.md.
        var minimumIal = minimumIalService.GetMinimumIal(household.SummerEbtCases);
        if (userIalLevel < minimumIal)
        {
            logger.LogInformation(
                "Card replacement denied: user IAL {UserIal} is below minimum {MinimumIal}",
                userIalLevel,
                minimumIal);
            return Result.Forbidden(
                $"This household requires {minimumIal}. Complete identity verification to request card replacements.");
        }

        // Co-loaded cases are managed by caseworkers, not the portal.
        var requestedCases = household.SummerEbtCases
            .Where(c => c.SummerEBTCaseID != null && command.CaseIds.Contains(c.SummerEBTCaseID));
        if (requestedCases.Any(c => c.IsCoLoaded))
        {
            logger.LogWarning(
                "Card replacement rejected: request includes co-loaded case(s)");
            return Result.PreconditionFailed(
                PreconditionFailedReason.Conflict,
                "Card replacements are not available for co-loaded benefits. Please contact your case worker.");
        }

        var cooldownErrors = CheckCooldown(command.CaseIds, household, timeProvider);
        if (cooldownErrors.Count > 0)
        {
            logger.LogInformation(
                "Card replacement rejected: {Count} case(s) within cooldown period",
                cooldownErrors.Count);
            return Result.ValidationFailed(cooldownErrors);
        }

        var identifierKind = identifier.Type.ToString();
        logger.LogInformation(
            "Card replacement request received for household identifier kind {Kind}, {Count} case(s)",
            identifierKind,
            command.CaseIds.Count);

        var pluginRequest = new PluginCardReplacementRequest
        {
            HouseholdIdentifierValue = identifier.Value,
            CaseIds = command.CaseIds,
            Reason = StatesPlugins.Interfaces.Models.Household.CardReplacementReason.Unspecified,
        };

        try
        {
            var connectorResult = await cardReplacementService.RequestCardReplacementAsync(
                pluginRequest,
                cancellationToken);

            if (connectorResult.IsSuccess)
            {
                logger.LogInformation(
                    "Card replacement request completed for household identifier kind {Kind}",
                    identifierKind);
                return Result.Success();
            }

            if (connectorResult.IsPolicyRejection)
            {
                logger.LogWarning(
                    "Card replacement policy rejection for household identifier kind {Kind}: {ErrorCode}",
                    identifierKind,
                    connectorResult.ErrorCode);
                return Result.PreconditionFailed(
                    PreconditionFailedReason.Conflict,
                    connectorResult.ErrorMessage);
            }

            logger.LogError(
                "Card replacement backend error for household identifier kind {Kind}: {ErrorCode}",
                identifierKind,
                connectorResult.ErrorCode);
            return Result.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                connectorResult.ErrorMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Card replacement plugin failed for household identifier kind {Kind}",
                identifierKind);
            return Result.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "Card replacement service is temporarily unavailable.");
        }
    }

    private static List<ValidationError> CheckCooldown(
        List<string> requestedCaseIds,
        Core.Models.Household.HouseholdData household,
        TimeProvider timeProvider)
    {
        var errors = new List<ValidationError>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var caseId in requestedCaseIds)
        {
            var summerEbtCase = household.SummerEbtCases
                .FirstOrDefault(c => c.SummerEBTCaseID == caseId);

            if (summerEbtCase == null)
            {
                errors.Add(new ValidationError(
                    "CaseIds",
                    $"Case {caseId} does not belong to this household."));
                continue;
            }

            if (summerEbtCase.CardRequestedAt == null)
                continue;

            var elapsed = now - summerEbtCase.CardRequestedAt.Value;
            if (elapsed < CooldownPeriod)
            {
                errors.Add(new ValidationError(
                    "CaseIds",
                    $"Case {caseId} was requested within the last 14 days."));
            }
        }

        return errors;
    }
}
