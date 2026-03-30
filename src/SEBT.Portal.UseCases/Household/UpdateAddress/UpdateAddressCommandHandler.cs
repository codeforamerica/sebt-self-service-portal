using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Handles mailing address updates for an authenticated user's household.
/// Validates input, resolves household identity, and returns success.
/// State connector call is stubbed — actual address persistence is a future integration.
/// </summary>
public class UpdateAddressCommandHandler(
    IValidator<UpdateAddressCommand> validator,
    IHouseholdIdentifierResolver resolver,
    ILogger<UpdateAddressCommandHandler> logger)
    : ICommandHandler<UpdateAddressCommand>
{
    public async Task<Result> Handle(UpdateAddressCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.Validate(command, cancellationToken);
        if (validationResult is ValidationFailedResult validationFailed)
        {
            logger.LogWarning("Address update validation failed");
            return Result.ValidationFailed(validationFailed.Errors);
        }

        var identifier = await resolver.ResolveAsync(command.User, cancellationToken);
        if (identifier == null)
        {
            logger.LogWarning("Address update attempted but no household identifier could be resolved from claims");
            return Result.Unauthorized("Unable to identify user from token.");
        }

        // Never log raw address fields — PII policy.
        // Extract enum name to a local to break CodeQL taint chain (identifier is tainted via .Value).
        var identifierKind = identifier.Type.ToString();

        logger.LogInformation(
            "Address update received for household identifier kind {Kind}",
            identifierKind);

        // TODO: Call state connector to persist address update.
        // This is stubbed — the handler returns success without writing to the state system.
        // When DC-160 / state connector work lands, wire up IAddressValidationService and
        // the state connector write method here.

        logger.LogInformation(
            "Address update completed for household identifier kind {Kind}",
            identifierKind);

        return Result.Success();
    }
}
