namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

public sealed record HouseholdLookupOperationConfig() : StateBackendReadOperationConfig
{
    /// <summary>
    /// How to build the outgoing lookup request body. Optional; when null the driver sends no
    /// body. See <see cref="RequestBinding"/> for the domain-centered constants + map vocabulary.
    /// </summary>
    public RequestBinding? Request { get; init; }

    /// <summary>
    /// How to map the backend's raw lookup response into canonical household data.
    /// </summary>
    public StateBackendResponseMapping? Response { get; init; }
}
