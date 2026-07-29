namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

public sealed record AddressUpdateOperationConfig() : StateBackendWriteOperationConfig
{
    /// <summary>How to build the outgoing address-update request body.</summary>
    public RequestBinding? Request { get; init; }

    /// <summary>How to classify the backend's response into a canonical address-update outcome.</summary>
    public ResultClassifier? Result { get; init; }
}
