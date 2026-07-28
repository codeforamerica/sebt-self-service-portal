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
    /// <summary>
    /// How the driver fans a child batch out to backend calls. <see cref="EnrollmentCallMode.Batch"/>
    /// makes ONE call carrying every child as a correlated row (CO); <see cref="EnrollmentCallMode.PerChild"/>
    /// loops the batch and makes ONE call per child (DC). Required — both samples must set it so the
    /// call shape is never inferred.
    /// </summary>
    public required EnrollmentCallMode CallMode { get; init; }

    /// <summary>How to build each outgoing request row from a child. Null when unmodeled.</summary>
    public EnrollmentRequestBinding? Request { get; init; }

    /// <summary>How to correlate + classify response rows back to children. Null when unmodeled.</summary>
    public EnrollmentResponseMapping? Response { get; init; }
}

/// <summary>
/// The closed set of enrollment call-fan-out modes. HARD CAP: exactly these two. A chunked batch
/// (max N children per call) would be a future <c>batchSize</c> variation — do NOT grow this enum
/// to model it without stopping first.
/// </summary>
public enum EnrollmentCallMode
{
    /// <summary>
    /// One backend call carrying every child as a correlated row. Children are correlated back by
    /// the request/response <c>indexField</c>, with expansion + any-candidate fan-in (CO).
    /// </summary>
    Batch,

    /// <summary>
    /// The driver loops the child batch and makes ONE call per child (request bound from that single
    /// child), reading a single result object per call — no correlation index (DC).
    /// </summary>
    PerChild,
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

    /// <summary>
    /// Dotted target path carrying the 1-based correlation index for the row. Required for
    /// <see cref="EnrollmentCallMode.Batch"/> (rows are correlated by index); MUST be null for
    /// <see cref="EnrollmentCallMode.PerChild"/> (each call is a single child — no index).
    /// </summary>
    public string? IndexField { get; init; }

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
/// Correlates response rows back to children and decides match. <see cref="Root"/> selects the row
/// array; <see cref="IndexField"/> is the source property carrying the echoed correlation index;
/// <see cref="Match"/> is a named-strategy brick selecting how a match is decided. Fan-in (a child
/// matches when ANY of its candidate rows matched) is implicit for batch mode.
/// </summary>
public sealed record EnrollmentResponseMapping
{
    /// <summary>Path to the array of response rows (e.g. <c>$.stdntDtls</c>). Same capped path grammar as reads.</summary>
    public required string Root { get; init; }

    /// <summary>
    /// Source property on each row carrying the echoed correlation index. Required for
    /// <see cref="EnrollmentCallMode.Batch"/> (verdicts fan in by index); MUST be null for
    /// <see cref="EnrollmentCallMode.PerChild"/> (one call reads a single result object — no index).
    /// </summary>
    public string? IndexField { get; init; }

    /// <summary>The named-strategy match brick deciding whether a child matches.</summary>
    public required EnrollmentMatch Match { get; init; }
}

/// <summary>
/// The closed set of enrollment match strategies. HARD CAP: exactly these two named strategies. The
/// argmax + strict <c>&gt;</c> comparison of <see cref="ConfidenceThreshold"/> live in fixed code —
/// config NEVER exposes comparison or boolean operators. If a real state needs a third shape, STOP
/// and add a NAMED strategy; do NOT add a general numeric-condition brick.
/// </summary>
public enum EnrollmentMatchStrategy
{
    /// <summary>
    /// A body field's value is in a set (the eligibility flag). Batch = per-row field∈set with
    /// any-candidate fan-in; PerChild = the single result's field∈set.
    /// </summary>
    AnyRowValueIn,

    /// <summary>
    /// A confidence score strictly exceeds a threshold. Batch = group a child's candidate rows by
    /// index, take the max score, match iff <c>max &gt; threshold</c> (mirrors CO's argmax + strict
    /// <c>&gt;</c>); PerChild = the single result's <c>score &gt; threshold</c> (no argmax needed).
    /// A missing/non-numeric score is not a match.
    /// </summary>
    ConfidenceThreshold,
}

/// <summary>
/// The named-strategy match brick. A flat record whose relevant fields depend on
/// <see cref="Strategy"/>: <see cref="AnyRowValueIn"/> uses <see cref="Field"/> + <see cref="ValueIn"/>;
/// <see cref="ConfidenceThreshold"/> uses <see cref="ScoreField"/> + <see cref="Threshold"/>. The
/// enrollment validator fails loud when the params for the chosen strategy are missing or the wrong
/// ones are supplied. NO comparison/boolean operators appear here — the <c>&gt;</c> lives in code.
/// </summary>
public sealed record EnrollmentMatch
{
    /// <summary>Which named strategy decides a match. Required — the shape is never inferred.</summary>
    public required EnrollmentMatchStrategy Strategy { get; init; }

    /// <summary>Source body property inspected on each row. Required for <see cref="EnrollmentMatchStrategy.AnyRowValueIn"/>.</summary>
    public string? Field { get; init; }

    /// <summary>
    /// The row matches when <see cref="Field"/>'s value is one of these (ordinal). Required for
    /// <see cref="EnrollmentMatchStrategy.AnyRowValueIn"/>.
    /// </summary>
    public List<string>? ValueIn { get; init; }

    /// <summary>Source body property carrying the numeric confidence score. Required for <see cref="EnrollmentMatchStrategy.ConfidenceThreshold"/>.</summary>
    public string? ScoreField { get; init; }

    /// <summary>
    /// The score must be strictly greater than this to match. Required for
    /// <see cref="EnrollmentMatchStrategy.ConfidenceThreshold"/>.
    /// </summary>
    public double? Threshold { get; init; }
}
