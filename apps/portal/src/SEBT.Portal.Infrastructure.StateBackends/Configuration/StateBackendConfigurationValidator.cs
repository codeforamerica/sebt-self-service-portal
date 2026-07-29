using SEBT.Portal.Core.StateBackends.Configuration;
using SEBT.Portal.Core.StateBackends.Configuration.Operations;
using SEBT.Portal.Infrastructure.StateBackends.Mapping;

namespace SEBT.Portal.Infrastructure.StateBackends.Configuration;

/// <summary>
/// The SINGLE load-time validation entry point for a state-backend config (DC-568 spike). Runs
/// immediately after deserialization in <see cref="StateBackendConfigurationLoader"/>, folding in
/// every config-shape check that previously fired lazily at first-request dispatch:
///
/// <list type="bullet">
///   <item>enum tables + keyword rules referenced by response mappings
///     (<see cref="StateBackendResponseMapper.ValidateEnumTables"/>);</item>
///   <item>each configured write op's result classifier
///     (<see cref="WriteResultClassifier.Validate"/>) — card replacement AND address update;</item>
///   <item>the enrollment op's call-mode / index-field / expand / match combination
///     (<see cref="EnrollmentOperationValidator.Validate"/>).</item>
/// </list>
///
/// Every check is a function of the loaded configuration ALONE — none reads per-request data — so a
/// malformed config fails loud at startup rather than on the first user request for that operation.
/// The underlying checks are reused verbatim so error semantics (exception type + message) are
/// preserved.
/// </summary>
internal static class StateBackendConfigurationValidator
{
    public static void Validate(StateBackendConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        // Enum tables / keyword rules referenced by any response mapping.
        StateBackendResponseMapper.ValidateEnumTables(configuration);

        StateBackendOperations operations = configuration.Operations;

        // Write-op result classifiers — validate every write op actually present.
        if (operations.CardReplacement?.Result is { } cardReplacementClassifier)
        {
            WriteResultClassifier.Validate(cardReplacementClassifier);
        }

        if (operations.AddressUpdate?.Result is { } addressUpdateClassifier)
        {
            WriteResultClassifier.Validate(addressUpdateClassifier);
        }

        // Enrollment op: only validatable when both binding and mapping are configured. A partially
        // modeled op (missing request/response) is left to the dispatch path's own not-supported
        // guards — there is nothing coherent to validate here.
        if (operations.EnrollmentCheck is { Request: { } binding, Response: { } mapping } enrollment)
        {
            EnrollmentOperationValidator.Validate(enrollment.CallMode, binding, mapping);
        }
    }
}
