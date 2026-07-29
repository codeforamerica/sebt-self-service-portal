namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Canonical write-outcome vocabulary for a state-backend write response (shared by the
/// card-replacement and address-update paths). A closed enum, not an expression language.
/// </summary>
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
/// Classifies a state-backend write response into a canonical <see cref="WriteOutcome"/>
/// (DC-568 spike). An ORDERED, first-match-wins list of <see cref="Conditions"/>; the first whose
/// predicate holds selects the outcome. Nothing matches → <see cref="Default"/>
/// (<see cref="WriteOutcome.BackendError"/> when unset).
///
/// HARD CAP: each condition is exactly ONE of three closed kinds (see <see cref="ResultCondition"/>).
/// No AND/OR combinators, no nesting. If a real case needs to combine conditions, STOP — do not
/// grow this into a rules engine.
/// </summary>
public sealed record ResultClassifier
{
    /// <summary>Ordered conditions; first match wins.</summary>
    public required List<ResultCondition> Conditions { get; init; }

    /// <summary>Outcome when no condition matches. Defaults to BackendError.</summary>
    public WriteOutcome Default { get; init; } = WriteOutcome.BackendError;
}

/// <summary>
/// One classifier condition. EXACTLY ONE of the three closed kinds must be set
/// (<see cref="StatusIn"/>, <see cref="ValueIn"/>, or <see cref="MessageContains"/>) — validated
/// fail-loud at load. The condition maps to an <see cref="Outcome"/> when it matches.
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
