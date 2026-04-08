using Microsoft.Extensions.Logging;
using SEBT.Portal.Core.Models.AddressUpdate;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Models.Household;
using SEBT.Portal.Core.Repositories;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Kernel;
using SEBT.Portal.Kernel.Results;
using SEBT.Portal.StatesPlugins.Interfaces;
using PluginAddress = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.Address;
using PluginAddressUpdateRequest = SEBT.Portal.StatesPlugins.Interfaces.Models.Household.AddressUpdateRequest;
using ICoreAddressUpdateService = SEBT.Portal.Core.Services.IAddressUpdateService;
using IStateAddressUpdateService = SEBT.Portal.StatesPlugins.Interfaces.IAddressUpdateService;

namespace SEBT.Portal.UseCases.Household;

/// <summary>
/// Handles mailing address updates for an authenticated user's household.
/// Validates input, normalizes the address via <see cref="ICoreAddressUpdateService"/>,
/// enforces benefit-type policy, and persists via state connector.
/// </summary>
public class UpdateAddressCommandHandler(
    IValidator<UpdateAddressCommand> validator,
    ICoreAddressUpdateService addressUpdateService,
    IAddressValidationService addressValidationService,
    IHouseholdIdentifierResolver resolver,
    IHouseholdRepository householdRepository,
    IIdProofingRequirementsService idProofingRequirementsService,
    IStateAddressUpdateService stateAddressUpdateService,
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

        // Validate and normalize address via Smarty (or pass-through when disabled).
        var addressRequest = new AddressUpdateOperationRequest
        {
            StreetAddress1 = command.StreetAddress1,
            StreetAddress2 = command.StreetAddress2,
            City = command.City,
            State = command.State,
            PostalCode = command.PostalCode
        };

        var addressOutcome = await addressUpdateService.ValidateAndNormalizeAsync(addressRequest, cancellationToken);
        Address? normalizedAddress = null;
        switch (addressOutcome)
        {
            case ValidationFailedResult<AddressUpdateSuccess> addressValidationFailed:
                // Smarty couldn't verify the address. Return a structured "not-found"
                // result so the frontend routes to the Address Not Found screen (422)
                // instead of showing a generic validation error (400).
                logger.LogWarning("Address update failed verification or policy checks");
                var firstError = addressValidationFailed.Errors.FirstOrDefault();
                return Result<AddressValidationResult>.Success(
                    AddressValidationResult.Invalid(
                        firstError?.Message ?? "Address could not be verified.",
                        "not-found"));
            case DependencyFailedResult<AddressUpdateSuccess> addressDependencyFailed:
                logger.LogWarning(
                    "Address verification dependency failed: {Reason}",
                    addressDependencyFailed.Reason);
                return Result<AddressValidationResult>.DependencyFailed(addressDependencyFailed.Reason, addressDependencyFailed.Message);
            case SuccessResult<AddressUpdateSuccess> success:
                normalizedAddress = success.Value.NormalizedAddress;
                break;
            default:
                return Result<AddressValidationResult>.DependencyFailed(
                    DependencyFailedReason.NotConfigured,
                    "Address verification failed.");
        }

        // Run state-specific address checks (blocked addresses, DC abbreviations, 30-char limit)
        // after Smarty normalization so we validate the canonical form.
        var stateValidation = await addressValidationService.ValidateAsync(
            normalizedAddress!, cancellationToken);
        if (!stateValidation.IsValid)
        {
            logger.LogInformation(
                "Address failed state-specific validation: {Reason}",
                stateValidation.Reason);
            return Result<AddressValidationResult>.Success(stateValidation);
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

        // Policy enforcement: SNAP and TANF households must update via case worker, not the portal.
        var userIalLevel = UserIalLevelExtensions.FromClaimsPrincipal(command.User);
        var piiVisibility = idProofingRequirementsService.GetPiiVisibility(userIalLevel);
        var household = await householdRepository.GetHouseholdByIdentifierAsync(
            identifier, piiVisibility, userIalLevel, cancellationToken);

        if (household is { BenefitIssuanceType: BenefitIssuanceType.SnapEbtCard or BenefitIssuanceType.TanfEbtCard })
        {
            logger.LogWarning(
                "Address update rejected for household identifier kind {Kind}: benefit type {BenefitType} is not eligible for portal self-service",
                identifierKind,
                household.BenefitIssuanceType);
            return Result<AddressValidationResult>.PreconditionFailed(
                PreconditionFailedReason.Conflict,
                "Address updates are not available for this benefit type. Please contact your case worker.");
        }

        // Use the normalized address from validation for the state connector call.
        var pluginAddress = new PluginAddress
        {
            StreetAddress1 = normalizedAddress!.StreetAddress1,
            StreetAddress2 = normalizedAddress.StreetAddress2,
            City = normalizedAddress.City,
            State = normalizedAddress.State,
            PostalCode = normalizedAddress.PostalCode
        };

        var updateRequest = new PluginAddressUpdateRequest
        {
            HouseholdIdentifierValue = identifier.Value,
            Address = pluginAddress
        };

        try
        {
            var updateResult = await stateAddressUpdateService.UpdateAddressAsync(updateRequest, cancellationToken);

            if (updateResult.IsSuccess)
            {
                logger.LogInformation("Address update completed for household identifier kind {Kind}", identifierKind);
                return Result<AddressValidationResult>.Success(
                    AddressValidationResult.Valid(normalizedAddress));
            }

            if (updateResult.IsPolicyRejection)
            {
                logger.LogWarning(
                    "Address update policy rejection for household identifier kind {Kind}: {ErrorCode}",
                    identifierKind,
                    updateResult.ErrorCode);
                return Result<AddressValidationResult>.PreconditionFailed(PreconditionFailedReason.Conflict, updateResult.ErrorMessage);
            }

            logger.LogError(
                "Address update backend error for household identifier kind {Kind}: {ErrorCode}",
                identifierKind,
                updateResult.ErrorCode);
            return Result<AddressValidationResult>.DependencyFailed(DependencyFailedReason.ConnectionFailed, updateResult.ErrorMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Address update plugin failed for household identifier kind {Kind}", identifierKind);
            return Result<AddressValidationResult>.DependencyFailed(
                DependencyFailedReason.ConnectionFailed,
                "Address update service is temporarily unavailable.");
        }
    }
}
