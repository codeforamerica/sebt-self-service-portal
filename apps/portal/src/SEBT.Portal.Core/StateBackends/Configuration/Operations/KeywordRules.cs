namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Classifies an enum-typed field by scanning its <see cref="FieldMapping.From"/> source(s)
/// for keyword substrings. Case-insensitive/first-match-wins.
/// </summary>
public sealed record KeywordRules
{
    /// <summary>
    /// Canonical enum values in evaluation order. First match wins, so a source that
    /// contains keywords for two values takes the earlier entry
    /// </summary>
    public required List<string> Order { get; init; }

    /// <summary>OUR canonical enum value → the substrings that indicate it. Empty keywords are rejected at load.</summary>
    public required Dictionary<string, List<string>> Map { get; init; }

    /// <summary>Canonical enum value used when no keyword matches any source.</summary>
    public required string Default { get; init; }
}
