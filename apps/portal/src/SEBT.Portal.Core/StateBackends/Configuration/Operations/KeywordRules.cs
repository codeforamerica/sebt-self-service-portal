namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// A contains-match inference brick (DC-568 spike). For an enum-typed field, classifies a canonical
/// value by scanning the field's <see cref="FieldMapping.From"/> source(s) for keyword substrings.
///
/// Semantics: evaluate the canonical values in <see cref="Order"/>; the first whose ANY substring
/// (from its <see cref="Map"/> entry) is contained in ANY source value wins. None match →
/// <see cref="Default"/>. Matching is case-insensitive, mirroring DC's <c>InferIssuanceType</c>.
///
/// Deliberately CAPPED: substring-contains over one-or-more named sources, first-match-wins,
/// ordered. NO regex, NO conditionals, NO transforms. If a state's inference needs more, STOP —
/// do not grow this into a matcher DSL.
/// </summary>
public sealed record KeywordRules
{
    /// <summary>
    /// Canonical enum values in evaluation order. First-match-wins, so order is load-bearing.
    /// Every entry must be a real member of the target enum, and must cover every <see cref="Map"/>
    /// key (validated at load, fail-loud).
    /// </summary>
    public required List<string> Order { get; init; }

    /// <summary>OUR canonical enum value → the substrings that indicate it.</summary>
    public required Dictionary<string, List<string>> Map { get; init; }

    /// <summary>Canonical enum value used when no keyword matches any source.</summary>
    public required string Default { get; init; }
}
