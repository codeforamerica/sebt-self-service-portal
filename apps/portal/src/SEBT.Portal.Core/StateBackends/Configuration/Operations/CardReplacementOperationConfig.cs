namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>How the driver fans a card-replacement batch out to backend calls.</summary>
public enum CardReplacementCallMode
{
    /// <summary>One backend call per decoded case token.</summary>
    PerCase,

    /// <summary>One backend call carrying every decoded case.</summary>
    Batch,
}

public sealed record CardReplacementOperationConfig() : StateBackendOperationConfig
{
    /// <summary>How the driver fans the case batch out; defaults to per-case.</summary>
    public CardReplacementCallMode CallMode { get; init; } = CardReplacementCallMode.PerCase;

    /// <summary>How to build the outgoing card-replacement request body; inputs include the decoded caseId fields plus the write envelope's <c>householdIdentifier</c>.</summary>
    public RequestBinding? Request { get; init; }

    /// <summary>How to classify the backend's response into a canonical card-replacement outcome.</summary>
    public ResultClassifier? Result { get; init; }
}
