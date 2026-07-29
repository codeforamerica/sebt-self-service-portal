using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;

namespace SEBT.Portal.Infrastructure.StateBackends.Configuration;

/// <summary>
/// The single load-time validation entry point for a state-backend config, run immediately after
/// deserialization in <see cref="StateBackendConfigurationLoader"/>. Every check is a function of
/// the loaded config alone, so a malformed config fails loud at startup rather than on the first
/// user request.
/// </summary>
internal static class StateBackendConfigurationValidator
{
    public static void Validate(StateBackendConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        StateBackendResponseMapper.ValidateEnumTables(configuration);

        StateBackendOperations operations = configuration.Operations;

        if (operations.CardReplacement?.Result is { } cardReplacementClassifier)
        {
            WriteResultClassifier.Validate(cardReplacementClassifier);
        }

        if (operations.AddressUpdate?.Result is { } addressUpdateClassifier)
        {
            WriteResultClassifier.Validate(addressUpdateClassifier);
        }

        // The write-path body builders don't read mapOptional yet, so a config setting it would be
        // a silent no-op. Fail loud instead; wire it through when a state actually needs it.
        RejectMapOptional(operations.CardReplacement?.Request, "cardReplacement");
        RejectMapOptional(operations.AddressUpdate?.Request, "addressUpdate");

        // A partially modeled enrollment op (missing request/response) has nothing coherent to
        // validate here; the dispatch path's not-supported guards catch it.
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
