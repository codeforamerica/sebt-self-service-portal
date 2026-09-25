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

        StateBackendOperations operations = configuration.Operations;

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

        // A partially modeled enrollment op is caught by the dispatch path's not-supported guards.
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
}
