namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Builds the outgoing request body from <see cref="Constants"/> (fixed literals) and <see cref="Map"/>
/// (our named input → dotted target path). The <see cref="Map"/> LHS includes <c>isProofed</c>, which
/// passes the caller's identity-proofing status through to the backend — a gate the backend relies on.
/// </summary>
public sealed record RequestBinding
{
    /// <summary>Dotted target path → fixed literal value (bool, number, string).</summary>
    public Dictionary<string, object>? Constants { get; init; }

    /// <summary>Our input name → dotted target path in the request body.</summary>
    public Dictionary<string, string>? Map { get; init; }

    /// <summary>
    /// Batch shape: a household-level routing field resolved once across every decoded caseId. The
    /// binder fails loud if the caseIds disagree on the value.
    /// </summary>
    public Dictionary<string, string>? Shared { get; init; }

    /// <summary>Batch shape: a per-case routing field gathered into an array, one element per decoded caseId.</summary>
    public Dictionary<string, string>? Collect { get; init; }
}
