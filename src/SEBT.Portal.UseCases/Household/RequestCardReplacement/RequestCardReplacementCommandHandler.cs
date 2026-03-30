using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Handles card replacement requests for an authenticated user.
/// Validates input, resolves household identity, enforces self-service rules,
/// and returns success. State connector call is stubbed for now.
/// </summary>
public class RequestCardReplacementCommandHandler(
    IValidator<RequestCardReplacementCommand> validator,
    IHouseholdIdentifierResolver resolver,
    IHouseholdRepository repository,
    ISelfServiceEvaluator selfServiceEvaluator,
    ILogger<RequestCardReplacementCommandHandler> logger)
    : ICommandHandler<RequestCardReplacementCommand>
{
    public async Task<Result> Handle(RequestCardReplacementCommand command, CancellationToken cancellationToken = default)
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
            logger.LogWarning("Card replacement attempted but no household identifier could be resolved from claims");
            return Result.Unauthorized("Unable to identify user from token.");
        }

        var householdData = await repository.GetHouseholdByIdentifierAsync(
            identifier,
            new PiiVisibility(IncludeAddress: false, IncludeEmail: false, IncludePhone: false),
            UserIalLevel.None,
            cancellationToken);

        if (householdData == null)
        {
            logger.LogWarning("Card replacement denied: household not found for identifier");
            return Result.PreconditionFailed(PreconditionFailedReason.NotAllowed, "Card replacement is not available.");
        }

        var allowedActions = selfServiceEvaluator.Evaluate(householdData.BenefitIssuanceType, householdData.Applications);
        if (!allowedActions.CanRequestReplacementCard)
        {
            logger.LogInformation("Card replacement denied by self-service rules for household");
            return Result.PreconditionFailed(PreconditionFailedReason.NotAllowed,
                allowedActions.CardReplacementDeniedMessageKey ?? "Card replacement is not available for this account.");
        }

        var identifierKind = identifier.Type.ToString();
        logger.LogInformation(
            "Card replacement request received for application {ApplicationNumber}, household identifier kind {Kind}",
            command.ApplicationNumber, identifierKind);

        // TODO: Call state connector to process card replacement.
        // Stubbed for now. When the state connector integration lands, wire up the actual call here.

        logger.LogInformation(
            "Card replacement completed for application {ApplicationNumber}, household identifier kind {Kind}",
            command.ApplicationNumber, identifierKind);

        return Result.Success();
    }
}
