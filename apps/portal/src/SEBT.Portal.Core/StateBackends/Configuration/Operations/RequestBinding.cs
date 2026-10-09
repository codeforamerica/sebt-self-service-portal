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

    /// <summary>
    /// Body is a JSON array with one object per decoded caseId. Each object is built from
    /// <see cref="Constants"/>, <see cref="Map"/>, and <see cref="MapOptional"/>, using the address
    /// scalars plus that case's routing fields. Cannot be combined with <see cref="Shared"/> or <see cref="Collect"/>.
    /// </summary>
    public bool EachCase { get; init; }
}
