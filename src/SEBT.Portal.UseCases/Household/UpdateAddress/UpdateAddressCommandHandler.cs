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
/// enforces self-service rules and benefit-type policy, and persists via state connector.
/// </summary>
public class UpdateAddressCommandHandler(
    IValidator<UpdateAddressCommand> validator,
    ICoreAddressUpdateService addressUpdateService,
    IAddressValidationService addressValidationService,
    IHouseholdIdentifierResolver resolver,
    IHouseholdRepository householdRepository,
    IPiiVisibilityService piiVisibilityService,
    IIdProofingService idProofingService,
    ISelfServiceEvaluator selfServiceEvaluator,
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
        var normalizationCorrectedAddress = false;
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
                normalizationCorrectedAddress = success.Value.WasCorrected;
                break;
            default:
                return Result<AddressValidationResult>.DependencyFailed(
                    DependencyFailedReason.NotConfigured,
                    "Address verification failed.");
        }

        // Run state-specific address checks (blocked addresses, DC abbreviations, 30-char limit)
        // after Smarty normalization so we validate the canonical form.
        var stateValidationOnNormalized = await addressValidationService.ValidateAsync(
            normalizedAddress!, cancellationToken);

        // Smarty's USPS-long canonical street can exceed DC's connector limit while the user's typed
        // street still fits. Abbreviated suggestions must not loop forever when the user explicitly
        // chooses "address you entered" - validate and persist those lines instead.
        var abbreviatedOnlyBlocksPersist = false;
        if (!stateValidationOnNormalized.IsValid)
        {
            // Only abbreviated-normalization mismatch may proceed; entered lines are validated below before persist.
            if (stateValidationOnNormalized.Reason != "abbreviated")
            {
                logger.LogInformation(
                    "Address failed state-specific validation: {Reason}",
                    stateValidationOnNormalized.Reason);
                return Result<AddressValidationResult>.Success(stateValidationOnNormalized);
            }

            if (!command.AcceptEnteredAddress)
            {
                logger.LogInformation(
                    "Address failed state-specific validation: {Reason}",
                    stateValidationOnNormalized.Reason);
                return Result<AddressValidationResult>.Success(stateValidationOnNormalized);
            }

            abbreviatedOnlyBlocksPersist = true;
        }

        Address persistAddress = normalizedAddress!;

        var enteredAddressModel = new Address
        {
            StreetAddress1 = command.StreetAddress1.Trim(),
            StreetAddress2 = string.IsNullOrWhiteSpace(command.StreetAddress2)
                ? null
                : command.StreetAddress2.Trim(),
            City = command.City.Trim(),
            State = command.State.Trim(),
            PostalCode = command.PostalCode.Trim()
        };

        if (command.AcceptEnteredAddress)
        {
            var enteredStateValidation =
                await addressValidationService.ValidateAsync(enteredAddressModel, cancellationToken);
            if (!enteredStateValidation.IsValid)
            {
                logger.LogInformation(
                    "User-opted entered address failed state-specific validation: {Reason}",
                    enteredStateValidation.Reason);
                return Result<AddressValidationResult>.Success(enteredStateValidation);
            }

            persistAddress = enteredAddressModel;
            logger.LogInformation(
                abbreviatedOnlyBlocksPersist
                    ? "Persisting user-entered address after opting out of abbreviated connector form"
                    : "Persisting user-entered address after opting out of normalization suggestion");
        }
        else if (normalizationCorrectedAddress)
        {
            logger.LogInformation("Address normalization produced a suggested alternative");
            return Result<AddressValidationResult>.Success(
                AddressValidationResult.Suggestion(normalizedAddress!, "suggested"));
        }

        var identifier = await resolver.ResolveAsync(command.User, cancellationToken);
        if (identifier == null)
        {
            logger.LogWarning("Address update attempted but no household identifier could be resolved from claims");
            return Result<AddressValidationResult>.Unauthorized("Unable to identify user from token.");
        }

        // Never log raw address fields - PII policy.
        // Extract enum name to a local to break CodeQL taint chain (identifier is tainted via .Value).
        var identifierKind = identifier.Type.ToString();

        logger.LogInformation(
            "Address update received for household identifier kind {Kind}",
            identifierKind);

        // Policy enforcement: SNAP and TANF households must update via case worker, not the portal.
        var userIalLevel = UserIalLevelExtensions.FromClaimsPrincipal(command.User);
        var piiVisibility = piiVisibilityService.GetVisibility(userIalLevel);
        var household = await householdRepository.GetHouseholdByIdentifierAsync(
            identifier, piiVisibility, userIalLevel, cancellationToken);

        if (household == null)
        {
            logger.LogWarning(
                "Address update attempted but household data not found for identifier kind {Kind}",
                identifierKind);
            return Result<AddressValidationResult>.PreconditionFailed(
                PreconditionFailedReason.NotFound,
                "Household data not found.");
        }

        // SECURITY: Block write operations when the user has not met the IAL
        // required by their cases. See docs/config/ial/README.md.
        var decision = idProofingService.Evaluate(
            ProtectedResource.Address, ProtectedAction.Write,
            userIalLevel, household.SummerEbtCases);
        if (!decision.IsAllowed)
        {
            logger.LogInformation(
                "Address update denied: user IAL {UserIal} is below required {RequiredIal}",
                userIalLevel,
                decision.RequiredLevel);
            return Result<AddressValidationResult>.Forbidden(
                $"This household requires {decision.RequiredLevel}. Complete identity verification to update your address.",
                new Dictionary<string, object?> { ["requiredIal"] = decision.RequiredLevel.ToString() });
        }

        if (household.SummerEbtCases.Count > 0)
        {
            // Mixed households: co-loaded cases are excluded from the self-service
            // permission surface; a household with any non-co-loaded case retains
            // address-update access. Only fully co-loaded households are blocked.
            var nonCoLoaded = household.SummerEbtCases.Where(c => !c.IsCoLoaded).ToList();
            if (nonCoLoaded.Count == 0)
            {
                logger.LogWarning(
                    "Address update rejected for household identifier kind {Kind}: household contains only co-loaded cases",
                    identifierKind);
                return Result<AddressValidationResult>.PreconditionFailed(
                    PreconditionFailedReason.Conflict,
                    "Address updates are not available for co-loaded benefits. Please contact your case worker.");
            }

            // Self-service rules enforcement: config-driven per state, issuance type, and card status.
            var allowedActions = selfServiceEvaluator.EvaluateHousehold(nonCoLoaded);
            if (!allowedActions.CanUpdateAddress)
            {
                logger.LogInformation("Address update denied by self-service rules for household");
                return Result<AddressValidationResult>.PreconditionFailed(
                    PreconditionFailedReason.NotAllowed,
                    allowedActions.AddressUpdateDeniedMessageKey ?? "Address update is not available for this account.");
            }
        }

        // Use the persisted address (normalized or user-entered when opted in) for the state connector call.
        var pluginAddress = new PluginAddress
        {
            StreetAddress1 = persistAddress.StreetAddress1,
            StreetAddress2 = persistAddress.StreetAddress2,
            City = persistAddress.City,
            State = persistAddress.State,
            PostalCode = persistAddress.PostalCode
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
                    AddressValidationResult.Valid(persistAddress));
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
