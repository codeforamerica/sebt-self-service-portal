namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>How the driver fans a child batch out to backend calls.</summary>
public enum EnrollmentCallMode
{
    /// <summary>One backend call carrying every child as a row correlated by <c>indexField</c>.</summary>
    Batch,

    /// <summary>The driver loops the batch, one call per child, reading a single result object each.</summary>
    PerChild,
}

/// <summary>Request-side candidate-expansion strategy.</summary>
public enum CandidateExpansion
{
    /// <summary>Exactly one request row per child.</summary>
    None,

    /// <summary>
    /// Emit the entered DOB plus its month/day-swapped candidate, but only when the swap yields a
    /// valid and different calendar date. Both rows share the child's correlation index.
    /// </summary>
    TransposeMonthDay,
}

/// <summary>How a match is decided for an enrollment check.</summary>
public enum EnrollmentMatchStrategy
{
    /// <summary>A body field's value is in a set. In batch mode a child matches if any of its rows match.</summary>
    AnyRowValueIn,

    /// <summary>
    /// A confidence score strictly exceeds a threshold; in batch mode the child's best candidate is
    /// taken. The argmax and the strict <c>&gt;</c> live in code. A missing/non-numeric score is not a match.
    /// </summary>
    ConfidenceThreshold,
}

/// <summary>
/// Turns a batch of children into backend calls (via <see cref="Request"/>) and a match verdict per
/// child (via <see cref="Response"/>).
/// </summary>
public sealed record EnrollmentCheckOperationConfig() : StateBackendOperationConfig
{
    /// <summary>How the driver fans a child batch out to backend calls. Required — never inferred.</summary>
    public required EnrollmentCallMode CallMode { get; init; }

    /// <summary>How to build each outgoing request row from a child. Null when unmodeled.</summary>
    public EnrollmentRequestBinding? Request { get; init; }

    /// <summary>How to correlate + classify response rows back to children. Null when unmodeled.</summary>
    public EnrollmentResponseMapping? Response { get; init; }
}

/// <summary>Builds the per-child request rows.</summary>
public sealed record EnrollmentRequestBinding
{
    /// <summary>Our child-field name (firstName / lastName / dob) → dotted target path in the request row.</summary>
    public required Dictionary<string, string> Map { get; init; }

    /// <summary>
    /// Dotted target path carrying the row's correlation index. Required for
    /// <see cref="EnrollmentCallMode.Batch"/>; must be null for <see cref="EnrollmentCallMode.PerChild"/>.
    /// </summary>
    public string? IndexField { get; init; }

    /// <summary>Candidate-expansion applied to the DOB input; <see cref="CandidateExpansion.None"/> emits one row per child.</summary>
    public CandidateExpansion Expand { get; init; } = CandidateExpansion.None;
}

/// <summary>Correlates response rows back to children and decides match.</summary>
public sealed record EnrollmentResponseMapping
{
    /// <summary>Path to the array of response rows (e.g. <c>$.stdntDtls</c>).</summary>
    public required string Root { get; init; }

    /// <summary>
    /// Source property carrying the echoed correlation index. Required for
    /// <see cref="EnrollmentCallMode.Batch"/>; must be null for <see cref="EnrollmentCallMode.PerChild"/>.
    /// </summary>
    public string? IndexField { get; init; }

    /// <summary>The match strategy deciding whether a child matches.</summary>
    public required EnrollmentMatch Match { get; init; }
}

/// <summary>
/// The match strategy and its params: <see cref="AnyRowValueIn"/> uses <see cref="Field"/> +
/// <see cref="ValueIn"/>; <see cref="ConfidenceThreshold"/> uses <see cref="ScoreField"/> +
/// <see cref="Threshold"/>. The validator fails loud on the wrong params for the chosen strategy.
/// </summary>
public sealed record EnrollmentMatch
{
    /// <summary>Which strategy decides a match. Required — never inferred.</summary>
    public required EnrollmentMatchStrategy Strategy { get; init; }

    /// <summary>Source body property inspected on each row. Required for <see cref="EnrollmentMatchStrategy.AnyRowValueIn"/>.</summary>
    public string? Field { get; init; }

    /// <summary>The row matches when <see cref="Field"/>'s value is one of these. Required for <see cref="EnrollmentMatchStrategy.AnyRowValueIn"/>.</summary>
    public List<string>? ValueIn { get; init; }

    /// <summary>Source body property carrying the numeric confidence score. Required for <see cref="EnrollmentMatchStrategy.ConfidenceThreshold"/>.</summary>
    public string? ScoreField { get; init; }

    /// <summary>The score must be strictly greater than this to match. Required for <see cref="EnrollmentMatchStrategy.ConfidenceThreshold"/>.</summary>
    public double? Threshold { get; init; }
}
