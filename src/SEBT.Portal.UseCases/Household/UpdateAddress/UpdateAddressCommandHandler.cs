using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Handles mailing address updates for an authenticated user's household.
/// Validates input, normalizes via <see cref="IAddressUpdateService"/>, resolves household identity,
/// validates against blocked lists and state-specific rules, and returns the validation result.
/// State connector persistence is stubbed — actual address write to the state system is a future integration.
/// </summary>
public class UpdateAddressCommandHandler(
    IValidator<UpdateAddressCommand> validator,
    IAddressUpdateService addressUpdateService,
    IHouseholdIdentifierResolver resolver,
    IAddressValidationService addressValidator,
    ILogger<UpdateAddressCommandHandler> logger)
    : ICommandHandler<UpdateAddressCommand, AddressValidationResult>
{
    public async Task<Result<AddressValidationResult>> Handle(UpdateAddressCommand command, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.Validate(command, cancellationToken);
        if (validationResult is ValidationFailedResult validationFailed)
        {
            logger.LogWarning("Address update validation failed");
            return Result<AddressValidationResult>.ValidationFailed(validationFailed.Errors);
        }

        var addressRequest = new AddressUpdateOperationRequest
        {
            StreetAddress1 = command.StreetAddress1,
            StreetAddress2 = command.StreetAddress2,
            City = command.City,
            State = command.State,
            PostalCode = command.PostalCode
        };

        // Phase 1: Smarty normalization. Capture the outcome but only bail early for
        // infrastructure failures. Validation results (not_found, corrected) are deferred
        // until after the state-specific checks in Phase 2.
        AddressUpdateSuccess? smartySuccess = null;
        string? smartyFailureMessage = null;
        IReadOnlyCollection<ValidationError>? smartyErrors = null;

        var addressOutcome = await addressUpdateService.ValidateAndNormalizeAsync(addressRequest, cancellationToken);
        switch (addressOutcome)
        {
            case ValidationFailedResult<AddressUpdateSuccess> addressValidationFailed:
                logger.LogWarning("Address update failed verification or policy checks");
                smartyErrors = addressValidationFailed.Errors;
                smartyFailureMessage = string.Join(" ", smartyErrors.Select(e => e.Message));
                break;
            case DependencyFailedResult<AddressUpdateSuccess> addressDependencyFailed:
                logger.LogWarning(
                    "Address verification dependency failed: {Reason}",
                    addressDependencyFailed.Reason);
                return Result<AddressValidationResult>.DependencyFailed(addressDependencyFailed.Reason, addressDependencyFailed.Message);
            case SuccessResult<AddressUpdateSuccess> addressSuccess:
                smartySuccess = addressSuccess.Value;
                break;
            default:
                return Result<AddressValidationResult>.DependencyFailed(
                    DependencyFailedReason.NotConfigured,
                    "Address verification failed.");
        }

        var identifier = await resolver.ResolveAsync(command.User, cancellationToken);
        if (identifier == null)
        {
            logger.LogWarning("Address update attempted but no household identifier could be resolved from claims");
            return Result<AddressValidationResult>.Unauthorized("Unable to identify user from token.");
        }

        // Never log raw address fields — PII policy.
        // Extract enum name to a local to break CodeQL taint chain (identifier is tainted via .Value).
        var identifierKind = identifier.Type.ToString();

        logger.LogInformation(
            "Address update received for household identifier kind {Kind}",
            identifierKind);

        var address = new Core.Models.Household.Address
        {
            StreetAddress1 = command.StreetAddress1,
            StreetAddress2 = command.StreetAddress2,
            City = command.City,
            State = command.State,
            PostalCode = command.PostalCode
        };

        // Phase 2: State-specific validation (blocked addresses, DC abbreviations, 30-char).
        // Runs on the original address regardless of Smarty's outcome. If this layer
        // rejects, its result takes priority over Smarty's.
        var addressValidation = await addressValidator.ValidateAsync(address, cancellationToken);
        if (!addressValidation.IsValid)
        {
            logger.LogInformation(
                "Address validation failed for household identifier kind {Kind}",
                identifierKind);
            return Result<AddressValidationResult>.Success(addressValidation);
        }

        // Phase 3: Combine results. State-specific checks passed, so fall back to Smarty's outcome.
        // Distinguish verification failures ("not_found") from policy rejections ("policy_violation").
        // Verification errors use key "address"; policy errors (e.g. General Delivery) use field-specific keys.
        if (smartyFailureMessage != null)
        {
            var reason = smartyErrors!.All(e => e.Key == "address") ? "not_found" : "policy_violation";
            return Result<AddressValidationResult>.Success(
                AddressValidationResult.Invalid(smartyFailureMessage, reason));
        }

        if (smartySuccess?.WasCorrected == true)
        {
            logger.LogInformation("Address verification returned a corrected address");
            return Result<AddressValidationResult>.Success(
                AddressValidationResult.Suggestion(smartySuccess.NormalizedAddress, "corrected"));
        }

        // TODO: Call state connector to persist normalized address.

        logger.LogInformation(
            "Address update completed for household identifier kind {Kind}",
            identifierKind);

        return Result<AddressValidationResult>.Success(AddressValidationResult.Valid());
    }
}
