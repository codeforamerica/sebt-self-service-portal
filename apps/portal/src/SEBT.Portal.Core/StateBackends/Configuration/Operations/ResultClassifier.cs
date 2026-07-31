namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>Canonical write-outcome vocabulary, shared by the card-replacement and address-update paths.</summary>
public enum WriteOutcome
{
    Success,

    /// <summary>The household is not eligible for the operation via the portal.</summary>
    PolicyRejection,

    /// <summary>The state backend returned an error.</summary>
    BackendError,
}

/// <summary>Classifies a write response into a <see cref="WriteOutcome"/>: ordered <see cref="Conditions"/>, first match wins.</summary>
public sealed record ResultClassifier
{
    public required List<ResultCondition> Conditions { get; init; }

    /// <summary>Outcome when no condition matches.</summary>
    public WriteOutcome Default { get; init; } = WriteOutcome.BackendError;
}

/// <summary>One classifier condition; exactly one of <see cref="StatusIn"/> / <see cref="ValueIn"/> / <see cref="MessageContains"/> — validated at load.</summary>
public sealed record ResultCondition
{
    public required WriteOutcome Outcome { get; init; }

    /// <summary>HTTP status code is in this set.</summary>
    public List<int>? StatusIn { get; init; }

    /// <summary>The body property <see cref="Field"/>'s value is in this set.</summary>
    public List<string>? ValueIn { get; init; }

    /// <summary>Body property inspected by <see cref="ValueIn"/>; required with it.</summary>
    public string? Field { get; init; }

    /// <summary>The body property <see cref="MessageField"/> contains any of these substrings (case-insensitive).</summary>
    public List<string>? MessageContains { get; init; }

    /// <summary>Body property scanned by <see cref="MessageContains"/>; required with it.</summary>
    public string? MessageField { get; init; }
}
