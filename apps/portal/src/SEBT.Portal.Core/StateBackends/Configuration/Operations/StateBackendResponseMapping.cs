namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Declares how to map a state backend's raw JSON response into canonical domain types.
/// Kept deliberately narrow (DC-568 spike): a root selector, a canonical-field → source-path
/// map, and an optional disaggregation primitive. NOT an arbitrary expression language.
/// </summary>
public sealed record StateBackendResponseMapping
{
    /// <summary>
    /// Path to the array of records within the raw response (e.g. <c>$.resultSets[0]</c>).
    /// Supports simple dotted property access and <c>[index]</c> element access only.
    /// </summary>
    public required string Root { get; init; }

    /// <summary>
    /// Canonical field name → how to pull and coerce it from the record selected by <see cref="Root"/>.
    /// </summary>
    public required Dictionary<string, FieldMapping> Fields { get; init; }

    /// <summary>
    /// Optional strategy for grouping records into applications and deciding case inclusion.
    /// Uses the closed vocabulary in <see cref="StateBackendDisaggregation"/>.
    /// </summary>
    public StateBackendDisaggregation? Disaggregation { get; init; }
}
