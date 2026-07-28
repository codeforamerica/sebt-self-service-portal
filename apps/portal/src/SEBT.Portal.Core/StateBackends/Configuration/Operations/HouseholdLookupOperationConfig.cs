namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

public sealed record HouseholdLookupOperationConfig() : StateBackendReadOperationConfig
{
    /// <summary>
    /// How to build the outgoing lookup request body: output field name → binding. Optional;
    /// when null the driver sends no body. Bindings pull from the request's identity signals
    /// or supply constants. See <see cref="RequestBinding"/> for the capped binding vocabulary.
    /// </summary>
    public Dictionary<string, RequestBinding>? Request { get; init; }

    /// <summary>
    /// How to map the backend's raw lookup response into canonical household data.
    /// </summary>
    public StateBackendResponseMapping? Response { get; init; }
}
