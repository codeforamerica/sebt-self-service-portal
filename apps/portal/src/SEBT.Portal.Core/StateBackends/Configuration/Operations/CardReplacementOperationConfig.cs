namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

public sealed record CardReplacementOperationConfig() : StateBackendWriteOperationConfig
{
    /// <summary>
    /// How to build the outgoing card-replacement request body. Uses the same domain-centered
    /// constants + map vocabulary as reads; the map's inputs include the fields decoded from the
    /// incoming opaque caseId token (see <see cref="RequestBinding"/>).
    /// </summary>
    public RequestBinding? Request { get; init; }

    /// <summary>
    /// How to classify the backend's response into a canonical card-replacement outcome.
    /// </summary>
    public ResultClassifier? Result { get; init; }
}
