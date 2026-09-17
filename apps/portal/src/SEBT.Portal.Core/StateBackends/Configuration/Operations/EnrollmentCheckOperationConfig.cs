namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>How the driver fans a child batch out to backend calls.</summary>
public enum EnrollmentCallMode
{
    /// <summary>One backend call carrying every child as a correlated row.</summary>
    Batch,

    /// <summary>One call per child.</summary>
    PerChild,
}

/// <summary>Request-side candidate-expansion strategy — closed set; add a named member, never operators.</summary>
public enum CandidateExpansion
{
    /// <summary>Exactly one request row per child.</summary>
    None,

    /// <summary>Also emit a month/day-swapped DOB row.</summary>
    TransposeMonthDay,
}

/// <summary>How a match is decided — closed set; add a named strategy, never operators.</summary>
public enum EnrollmentMatchStrategy
{
    /// <summary>A row field's value is in a set.</summary>
    AnyRowValueIn,

    /// <summary>The best row's score strictly exceeds a threshold.</summary>
    ConfidenceThreshold,
}

/// <summary>The enrollmentCheck operation: batch fan-out plus a per-child match verdict.</summary>
public sealed record EnrollmentCheckOperationConfig() : StateBackendOperationConfig
{
    public required EnrollmentCallMode CallMode { get; init; }

    /// <summary>How to build each outgoing request row from a child.</summary>
    public EnrollmentRequestBinding? Request { get; init; }

    /// <summary>How to correlate response rows back to children and classify them.</summary>
    public EnrollmentResponseMapping? Response { get; init; }
}

/// <summary>Builds the per-child request rows.</summary>
public sealed record EnrollmentRequestBinding
{
    /// <summary>Our child-field name → dotted target path in the request row.</summary>
    public required Dictionary<string, string> Map { get; init; }

    /// <summary>Like <see cref="Map"/>, but an unresolved input is omitted from the row (never written as null).</summary>
    public Dictionary<string, string>? MapOptional { get; init; }

    /// <summary>Dotted target path carrying the row's correlation index; required for batch, must be null for perChild — validated at load.</summary>
    public string? IndexField { get; init; }

    /// <summary>Candidate expansion applied to the DOB input.</summary>
    public CandidateExpansion Expand { get; init; } = CandidateExpansion.None;
}

/// <summary>Correlates response rows back to children and decides match.</summary>
public sealed record EnrollmentResponseMapping
{
    /// <summary>Path to the array of response rows.</summary>
    public required string Root { get; init; }

    /// <summary>Source property carrying the echoed correlation index; required for batch, must be null for perChild — validated at load.</summary>
    public string? IndexField { get; init; }

    /// <summary>Row property surfaced as the child's <c>StatusMessage</c> — per-row, unlike <see cref="MessageField"/>.</summary>
    public string? StatusMessageField { get; init; }

    /// <summary>Result-level property surfaced as <c>EnrollmentCheckResult.Message</c> — read from the response root, not per-row.</summary>
    public string? MessageField { get; init; }

    public required EnrollmentMatch Match { get; init; }
}

/// <summary>The match strategy and its params; wrong params for the chosen strategy fail at load.</summary>
public sealed record EnrollmentMatch
{
    public required EnrollmentMatchStrategy Strategy { get; init; }

    /// <summary>Row property inspected: required for anyRowValueIn, an optional eligibility check (with <see cref="ValueIn"/>) for confidenceThreshold.</summary>
    public string? Field { get; init; }

    /// <summary>Passing values for <see cref="Field"/>; the pair comes together or not at all — validated at load.</summary>
    public List<string>? ValueIn { get; init; }

    /// <summary>Row property carrying the numeric confidence score; confidenceThreshold only.</summary>
    public string? ScoreField { get; init; }

    /// <summary>The score must strictly exceed this to match; confidenceThreshold only.</summary>
    public double? Threshold { get; init; }
}
