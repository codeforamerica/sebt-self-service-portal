namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Classifies an enum-typed field by scanning its <see cref="FieldMapping.From"/> source(s) for
/// keyword substrings, first-match-wins over <see cref="Order"/>, case-insensitive.
/// </summary>
public sealed record KeywordRules
{
    /// <summary>Canonical enum values in evaluation order; first-match-wins, so order is load-bearing.</summary>
    public required List<string> Order { get; init; }

    /// <summary>OUR canonical enum value → the substrings that indicate it.</summary>
    public required Dictionary<string, List<string>> Map { get; init; }

    /// <summary>Canonical enum value used when no keyword matches any source.</summary>
    public required string Default { get; init; }
}
