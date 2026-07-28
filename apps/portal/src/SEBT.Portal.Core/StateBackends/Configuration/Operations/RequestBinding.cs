namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Maps a single output request field to exactly one source. A CLOSED, capped primitive
/// (DC-568 spike): only three mutually-exclusive binding kinds are supported.
///   * <see cref="From"/>   — pull the value of the identity signal whose type matches.
///   * <see cref="Const"/>  — a fixed literal value.
///   * <see cref="Compose"/> — build a nested object from named sub-bindings (recursive).
///
/// Modeled as a concrete record with nullable fields rather than a polymorphic type because
/// YamlDotNet cannot hydrate polymorphic bindings cleanly. Exactly one field must be set;
/// this invariant is validated at load time (fail-loud).
/// </summary>
public sealed record RequestBinding
{
    /// <summary>Identity-signal type whose value populates this field (e.g. <c>email</c>).</summary>
    public string? From { get; init; }

    /// <summary>A fixed literal value (string, bool, number).</summary>
    public object? Const { get; init; }

    /// <summary>Named sub-bindings composing a nested object.</summary>
    public Dictionary<string, RequestBinding>? Compose { get; init; }
}
