namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

public sealed record CardReplacementOperationConfig() : StateBackendWriteOperationConfig
{
    /// <summary>How to build the outgoing card-replacement request body; inputs include the decoded caseId fields.</summary>
    public RequestBinding? Request { get; init; }

    /// <summary>How to classify the backend's response into a canonical card-replacement outcome.</summary>
    public ResultClassifier? Result { get; init; }
}
