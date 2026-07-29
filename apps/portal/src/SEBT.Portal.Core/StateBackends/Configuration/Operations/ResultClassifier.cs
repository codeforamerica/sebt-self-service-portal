namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>Canonical write-outcome vocabulary, shared by the card-replacement and address-update paths.</summary>
public enum WriteOutcome
{
    /// <summary>The replacement was initiated successfully.</summary>
    Success,

    /// <summary>The household is not eligible to request a replacement via the portal.</summary>
    PolicyRejection,

    /// <summary>The state backend returned an error.</summary>
    BackendError,
}

/// <summary>
/// Classifies a state-backend write response into a canonical <see cref="WriteOutcome"/> via an
/// ordered, first-match-wins list of <see cref="Conditions"/>.
/// </summary>
public sealed record ResultClassifier
{
    /// <summary>Ordered conditions; first match wins.</summary>
    public required List<ResultCondition> Conditions { get; init; }

    /// <summary>Outcome when no condition matches. Defaults to BackendError.</summary>
    public WriteOutcome Default { get; init; } = WriteOutcome.BackendError;
}

/// <summary>
/// One classifier condition; exactly one of <see cref="StatusIn"/> / <see cref="ValueIn"/> /
/// <see cref="MessageContains"/> must be set (validated fail-loud at load).
/// </summary>
public sealed record ResultCondition
{
    /// <summary>Outcome selected when this condition matches.</summary>
    public required WriteOutcome Outcome { get; init; }

    /// <summary>Kind 1 — HTTP status code is in this set.</summary>
    public List<int>? StatusIn { get; init; }

    /// <summary>Kind 2 — the response body field <see cref="Field"/>'s value is in this set.</summary>
    public List<string>? ValueIn { get; init; }

    /// <summary>Source body property inspected by <see cref="ValueIn"/> (required for that kind).</summary>
    public string? Field { get; init; }

    /// <summary>Kind 3 — the response message contains ANY of these substrings (case-insensitive).</summary>
    public List<string>? MessageContains { get; init; }

    /// <summary>Body property scanned by <see cref="MessageContains"/> (required for that kind).</summary>
    public string? MessageField { get; init; }
}
