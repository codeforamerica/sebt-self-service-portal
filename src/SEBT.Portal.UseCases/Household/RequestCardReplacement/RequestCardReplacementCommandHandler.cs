using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Handles card replacement requests for an authenticated user's household.
/// Validates input, resolves household identity, enforces 2-week cooldown, and returns success.
/// State connector call is stubbed — actual card replacement is a future integration.
/// </summary>
public class RequestCardReplacementCommandHandler(
    IValidator<RequestCardReplacementCommand> validator,
    IHouseholdIdentifierResolver resolver,
    IHouseholdRepository repository,
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

        var cooldownErrors = CheckCooldown(command.ApplicationNumbers, household, timeProvider);
        if (cooldownErrors.Count > 0)
        {
            logger.LogInformation(
                "Card replacement rejected: {Count} application(s) within cooldown period",
                cooldownErrors.Count);
            return Result.ValidationFailed(cooldownErrors);
        }

        var identifierKind = identifier.Type.ToString();
        logger.LogInformation(
            "Card replacement request received for household identifier kind {Kind}, {Count} application(s)",
            identifierKind,
            command.ApplicationNumbers.Count);

        // TODO: Call state connector to process card replacement.
        // Stubbed — returns success without calling the state system.

        logger.LogInformation(
            "Card replacement request recorded for household identifier kind {Kind}",
            identifierKind);

        return Result.Success();
    }

    private static List<ValidationError> CheckCooldown(
        List<string> requestedApplicationNumbers,
        Core.Models.Household.HouseholdData household,
        TimeProvider timeProvider)
    {
        var errors = new List<ValidationError>();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        foreach (var appNumber in requestedApplicationNumbers)
        {
            var application = household.Applications
                .FirstOrDefault(a => a.ApplicationNumber == appNumber);

            if (application == null)
            {
                errors.Add(new ValidationError(
                    "ApplicationNumbers",
                    $"Application {appNumber} does not belong to this household."));
                continue;
            }

            if (application.CardRequestedAt == null)
                continue;

            var elapsed = now - application.CardRequestedAt.Value;
            if (elapsed < CooldownPeriod)
            {
                errors.Add(new ValidationError(
                    "ApplicationNumbers",
                    $"Application {appNumber} was requested within the last 14 days."));
            }
        }

        return errors;
    }
}
