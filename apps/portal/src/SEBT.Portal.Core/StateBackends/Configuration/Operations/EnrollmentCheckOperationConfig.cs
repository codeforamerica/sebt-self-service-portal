namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Enrollment-check operation config (DC-568 spike). Domain-centered on the enrollment op: a
/// <see cref="Request"/> that turns each child into one or more backend request rows (optionally
/// expanding DOB candidates under a shared correlation index) and a <see cref="Response"/> that
/// classifies each returned row as a match and fans those verdicts back in by the correlation index
/// (a child matches when ANY of its candidate rows matched).
///
/// The two bricks this op adds — closed DOB candidate <see cref="EnrollmentRequestBinding.Expand"/>
/// and any-candidate fan-in — are the HARD CAP. Nothing here expresses match-confidence thresholds,
/// fuzzy/approximate name matching, or per-child orchestration beyond expand + fan-in. If a real
/// state needs more, STOP — do not grow this into a matching engine.
/// </summary>
public sealed record EnrollmentCheckOperationConfig() : StateBackendReadOperationConfig
{
    /// <summary>How to build each outgoing request row from a child. Null when unmodeled.</summary>
    public EnrollmentRequestBinding? Request { get; init; }

    /// <summary>How to correlate + classify response rows back to children. Null when unmodeled.</summary>
    public EnrollmentResponseMapping? Response { get; init; }
}

/// <summary>
/// Builds the per-child request rows. <see cref="Map"/> is OUR child-field name → dotted target path
/// in the backend row (closed LHS: firstName / lastName / dob). <see cref="IndexField"/> is the
/// dotted target path where the row's correlation index is written so the backend can echo it back.
/// <see cref="Expand"/> optionally emits extra candidate rows for one child under the SAME index.
/// </summary>
public sealed record EnrollmentRequestBinding
{
    /// <summary>OUR child-field name → dotted target path in the request row.</summary>
    public required Dictionary<string, string> Map { get; init; }

    /// <summary>Dotted target path carrying the 1-based correlation index for the row.</summary>
    public required string IndexField { get; init; }

    /// <summary>
    /// Optional, closed candidate-expansion strategy applied to the DOB input. Null (or
    /// <see cref="CandidateExpansion.None"/>) emits exactly one row per child.
    /// </summary>
    public CandidateExpansion Expand { get; init; } = CandidateExpansion.None;
}

/// <summary>
/// The closed set of request-side candidate-expansion strategies. A named brick, NOT a
/// date-mangling mini-language.
/// </summary>
public enum CandidateExpansion
{
    /// <summary>Exactly one request row per child.</summary>
    None,

    /// <summary>
    /// Emit the entered DOB PLUS its month/day-swapped candidate, but ONLY when the swap yields a
    /// valid AND different calendar date. Both rows share the child's correlation index.
    /// </summary>
    TransposeMonthDay,
}

/// <summary>
/// Correlates response rows back to children and decides per-row match. <see cref="Root"/> selects
/// the row array; <see cref="IndexField"/> is the source property carrying the echoed correlation
/// index; <see cref="MatchWhen"/> is a single closed predicate (a body field's value in a set — the
/// eligibility flag) deciding whether ONE row is a match. Fan-in (a child matches when ANY of its
/// rows matched) is implicit.
/// </summary>
public sealed record EnrollmentResponseMapping
{
    /// <summary>Path to the array of response rows (e.g. <c>$.stdntDtls</c>). Same capped path grammar as reads.</summary>
    public required string Root { get; init; }

    /// <summary>Source property on each row carrying the echoed correlation index.</summary>
    public required string IndexField { get; init; }

    /// <summary>The single closed predicate deciding whether ONE row is a match.</summary>
    public required EnrollmentMatchCondition MatchWhen { get; init; }
}

/// <summary>
/// The row-level match predicate: a body <see cref="Field"/>'s value is in <see cref="ValueIn"/>.
/// This is the write-classifier's <c>valueIn(field)</c> kind reused as a boolean predicate — no
/// outcome routing, no numeric thresholds, no fuzzy matching. HARD CAP: this is the ONLY row-match
/// shape. If a real state needs confidence scoring or approximate matching, STOP.
/// </summary>
public sealed record EnrollmentMatchCondition
{
    /// <summary>Source body property inspected on each row.</summary>
    public required string Field { get; init; }

    /// <summary>The row matches when <see cref="Field"/>'s value is one of these (ordinal).</summary>
    public required List<string> ValueIn { get; init; }
}
