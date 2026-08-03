namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>Builds the outgoing request body; a mapped <c>isProofed</c> input passes the caller's identity-proofing status to the backend — a gate the backend relies on.</summary>
public sealed record RequestBinding
{
    /// <summary>Dotted target path → fixed literal value (bool, number, string).</summary>
    public Dictionary<string, object>? Constants { get; init; }

    /// <summary>Our input name → dotted target path in the request body.</summary>
    public Dictionary<string, string>? Map { get; init; }

    /// <summary>Like <see cref="Map"/>, but an unresolved input is omitted from the body (never written as null).</summary>
    public Dictionary<string, string>? MapOptional { get; init; }

    /// <summary>Batch shape: a household-level routing field resolved once across every decoded caseId; fails loud if the caseIds disagree.</summary>
    public Dictionary<string, string>? Shared { get; init; }

    /// <summary>Batch shape: a per-case routing field gathered into an array, one element per decoded caseId.</summary>
    public Dictionary<string, string>? Collect { get; init; }
}
