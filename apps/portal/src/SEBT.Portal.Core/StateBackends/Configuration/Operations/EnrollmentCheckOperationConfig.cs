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
    /// Also emit a month/day-swapped DOB row when the swap is a valid, different date; both rows
    /// share the child's correlation index.
    /// </summary>
    TransposeMonthDay,
}

/// <summary>How a match is decided for an enrollment check.</summary>
public enum EnrollmentMatchStrategy
{
    /// <summary>A body field's value is in a set. In batch mode a child matches if any of its rows match.</summary>
    AnyRowValueIn,

    /// <summary>
    /// The best candidate's score strictly exceeds a threshold; a missing/non-numeric score is not a
    /// match. An optional <c>field</c> + <c>valueIn</c> pair adds an AND eligibility check on that row.
    /// </summary>
    ConfidenceThreshold,
}

/// <summary>
/// Turns a batch of children into backend calls (via <see cref="Request"/>) and a match verdict per
/// child (via <see cref="Response"/>).
/// </summary>
public sealed record EnrollmentCheckOperationConfig() : StateBackendOperationConfig
{
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

    /// <summary>Like <see cref="Map"/>, but an unresolved input is omitted from the row (never written as null).</summary>
    public Dictionary<string, string>? MapOptional { get; init; }

    /// <summary>
    /// Dotted target path carrying the row's correlation index; required for batch mode, must be
    /// null for perChild.
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
    /// Source property carrying the echoed correlation index; required for batch mode, must be null
    /// for perChild.
    /// </summary>
    public string? IndexField { get; init; }

    /// <summary>Optional source row property surfaced as the child's <c>StatusMessage</c>, read from the winning row.</summary>
    public string? StatusMessageField { get; init; }

    /// <summary>Optional result-level property surfaced as <c>EnrollmentCheckResult.Message</c>, read from the response root — not per-row.</summary>
    public string? MessageField { get; init; }

    /// <summary>The match strategy deciding whether a child matches.</summary>
    public required EnrollmentMatch Match { get; init; }
}

/// <summary>
/// The match strategy and its params; the validator fails loud on the wrong params for the chosen
/// strategy.
/// </summary>
public sealed record EnrollmentMatch
{
    public required EnrollmentMatchStrategy Strategy { get; init; }

    /// <summary>Source body property inspected on a row; required for anyRowValueIn, an optional best-row eligibility check (with <see cref="ValueIn"/>) for confidenceThreshold.</summary>
    public string? Field { get; init; }

    /// <summary>The row passes when <see cref="Field"/>'s value is one of these; supplied together with <see cref="Field"/> or not at all.</summary>
    public List<string>? ValueIn { get; init; }

    /// <summary>Source body property carrying the numeric confidence score; required for confidenceThreshold.</summary>
    public string? ScoreField { get; init; }

    /// <summary>The score must be strictly greater than this to match; required for confidenceThreshold.</summary>
    public double? Threshold { get; init; }
}
