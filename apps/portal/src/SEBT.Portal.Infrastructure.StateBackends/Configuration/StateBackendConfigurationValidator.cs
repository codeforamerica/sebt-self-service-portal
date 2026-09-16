using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;

namespace SEBT.Portal.Infrastructure.StateBackends.Configuration;

/// <summary>
/// The single load-time validation entry point for a state-backend config: a malformed config
/// fails loud at startup rather than on the first user request.
/// </summary>
internal static class StateBackendConfigurationValidator
{
    public static void Validate(StateBackendConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        StateBackendResponseMapper.ValidateFieldMappings(configuration);
        StateBackendResponseMapper.ValidateEnumTables(configuration);
        StateBackendResponseMapper.ValidateCaseIdCompositions(configuration);
        StateBackendResponseMapper.ValidateDisaggregation(configuration);

        StateBackendOperations operations = configuration.Operations;

        RejectIncompleteWrite(operations.CardReplacement, "cardReplacement");
        RejectIncompleteWrite(operations.AddressUpdate, "addressUpdate");
        RejectIncompleteEnrollment(operations.EnrollmentCheck);
        RejectIncompleteLookup(operations.HouseholdLookup);

        if (operations.CardReplacement?.Result is { } cardReplacementClassifier)
        {
            WriteResultClassifier.Validate(cardReplacementClassifier);
        }

        if (operations.AddressUpdate?.Result is { } addressUpdateClassifier)
        {
            WriteResultClassifier.Validate(addressUpdateClassifier);
        }

        // Write-path body builders don't read mapOptional yet; fail loud rather than silently no-op.
        RejectMapOptional(operations.CardReplacement?.Request, "cardReplacement");
        RejectMapOptional(operations.AddressUpdate?.Request, "addressUpdate");

        if (operations.EnrollmentCheck is { Request: { } binding, Response: { } mapping } enrollment)
        {
            EnrollmentOperationValidator.Validate(enrollment.CallMode, binding, mapping);
        }
    }

    private static void RejectMapOptional(RequestBinding? request, string operationName)
    {
        if (request?.MapOptional is { Count: > 0 })
        {
            throw new InvalidOperationException(
                $"mapOptional is not supported on write operations ({operationName}).");
        }
    }

    private static void RejectIncompleteWrite(StateBackendOperationConfig? operation, string operationName)
    {
        if (operation is null)
        {
            return;
        }

        RequestBinding? request = operation switch
        {
            CardReplacementOperationConfig card => card.Request,
            AddressUpdateOperationConfig address => address.Request,
            _ => null,
        };
        ResultClassifier? result = operation switch
        {
            CardReplacementOperationConfig card => card.Result,
            AddressUpdateOperationConfig address => address.Result,
            _ => null,
        };

        if (request is null || result is null)
        {
            throw new InvalidOperationException(
                $"Operation '{operationName}' is incomplete: both 'request' and 'result' are required " +
                "when the operation is declared. Omit the operation entirely to disable the feature.");
        }
    }

    private static void RejectIncompleteEnrollment(EnrollmentCheckOperationConfig? enrollment)
    {
        if (enrollment is null)
        {
            return;
        }

        if (enrollment.Request is null || enrollment.Response is null)
        {
            throw new InvalidOperationException(
                "Operation 'enrollmentCheck' is incomplete: both 'request' and 'response' are required " +
                "when the operation is declared. Omit the operation entirely to disable the feature.");
        }
    }

    private static void RejectIncompleteLookup(HouseholdLookupOperationConfig? lookup)
    {
        if (lookup is null)
        {
            return;
        }

        if (lookup.Response is null)
        {
            throw new InvalidOperationException(
                "Operation 'householdLookup' is incomplete: 'response' is required when the operation is declared.");
        }
    }
}
