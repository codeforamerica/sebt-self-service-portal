namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Domain-centered request binding (DC-568 spike). Describes how to build the outgoing lookup
/// request body from two sources:
///   * <see cref="Constants"/> — fixed state scaffolding with no domain source. Keys are dotted
///     target paths in the state's request body; values are literals (bool, number, string).
///   * <see cref="Map"/> — OUR named input → dotted target path in the state's request body. The
///     LHS is a closed set: household-identity signal types (email/ic/dob/phone) plus a small,
///     fixed set of caller-context names (isProofed, portalUuid). The binder resolves the input
///     value and writes it at the dotted target path.
///
/// Nesting is expressed by dotted target paths on the RHS; the binder builds the nested JSON.
/// There is no composition brick and no expression/transform vocabulary — resolution is a closed
/// set of pass-through inputs and constants. A map input that resolves to nothing fails loud.
/// </summary>
public sealed record RequestBinding
{
    /// <summary>Dotted target path → fixed literal value (bool, number, string).</summary>
    public Dictionary<string, object>? Constants { get; init; }

    /// <summary>OUR input name → dotted target path in the state's request body.</summary>
    public Dictionary<string, string>? Map { get; init; }

    /// <summary>
    /// BATCH shape (address update). A household-level routing field resolved ONCE across every
    /// decoded caseId — LHS is a decoded routing-field name, RHS is a dotted target path. The
    /// binder FAILS LOUD if the caseIds disagree on the value (DC: the shared household email).
    /// </summary>
    public Dictionary<string, string>? Shared { get; init; }

    /// <summary>
    /// BATCH shape (address update). A per-case routing field gathered into an ARRAY at a dotted
    /// target path — LHS is a decoded routing-field name, RHS is the array's target path. One
    /// array element per decoded caseId (CO: the per-case write-ids into the PATCH array).
    ///
    /// HARD CAP: <see cref="Shared"/> + <see cref="Collect"/> are the ONLY batch shapes. No
    /// per-case conditionals, filtering, or transforms. If a real case needs more, STOP.
    /// </summary>
    public Dictionary<string, string>? Collect { get; init; }
}
