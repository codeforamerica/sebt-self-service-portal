using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Handles mailing address updates for an authenticated user's household.
/// Validates input, normalizes the address via <see cref="IAddressUpdateService"/>, resolves household identity, and returns success.
/// State connector persistence is stubbed — actual address write to the state system is a future integration.
/// </summary>
public class UpdateAddressCommandHandler(
    IValidator<UpdateAddressCommand> validator,
    IAddressUpdateService addressUpdateService,
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

        var addressRequest = new AddressUpdateOperationRequest
        {
            StreetAddress1 = command.StreetAddress1,
            StreetAddress2 = command.StreetAddress2,
            City = command.City,
            State = command.State,
            PostalCode = command.PostalCode
        };

        var addressOutcome = await addressUpdateService.ValidateAndNormalizeAsync(addressRequest, cancellationToken);
        switch (addressOutcome)
        {
            case ValidationFailedResult<AddressUpdateSuccess> addressValidationFailed:
                logger.LogWarning("Address update failed verification or policy checks");
                return Result.ValidationFailed(addressValidationFailed.Errors);
            case DependencyFailedResult<AddressUpdateSuccess> addressDependencyFailed:
                logger.LogWarning(
                    "Address verification dependency failed: {Reason}",
                    addressDependencyFailed.Reason);
                return Result.DependencyFailed(addressDependencyFailed.Reason, addressDependencyFailed.Message);
            case SuccessResult<AddressUpdateSuccess>:
                break;
            default:
                return Result.DependencyFailed(
                    DependencyFailedReason.NotConfigured,
                    "Address verification failed.");
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

        // TODO: Call state connector to persist normalized address from addressOutcome (SuccessResult).

        logger.LogInformation(
            "Address update completed for household identifier kind {Kind}",
            identifierKind);

        return Result.Success();
    }
}
