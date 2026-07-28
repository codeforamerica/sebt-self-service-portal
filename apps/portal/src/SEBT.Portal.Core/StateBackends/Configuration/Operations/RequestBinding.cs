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
}
