namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

public sealed record HouseholdLookupOperationConfig() : StateBackendOperationConfig
{
    /// <summary>How to build the outgoing lookup request body; when null the driver sends no body.</summary>
    public RequestBinding? Request { get; init; }

    /// <summary>How to map the backend's raw lookup response into canonical household data.</summary>
    public StateBackendResponseMapping? Response { get; init; }
}
