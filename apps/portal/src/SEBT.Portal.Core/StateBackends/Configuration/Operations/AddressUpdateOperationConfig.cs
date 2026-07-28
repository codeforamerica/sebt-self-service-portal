namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

public sealed record AddressUpdateOperationConfig() : StateBackendWriteOperationConfig
{
    /// <summary>
    /// How to build the outgoing address-update request body. Uses the same domain-centered
    /// constants + map vocabulary as other writes, plus the two BATCH shapes address update needs
    /// (see <see cref="RequestBinding.Shared"/> and <see cref="RequestBinding.Collect"/>). The
    /// scalar map's inputs are the decoded caseId routing fields plus the address scalars
    /// (line1/line2/city/state/zip).
    /// </summary>
    public RequestBinding? Request { get; init; }

    /// <summary>
    /// How to classify the backend's response into a canonical address-update outcome. Reuses the
    /// same capped 3-kind classifier as card replacement.
    /// </summary>
    public ResultClassifier? Result { get; init; }
}
