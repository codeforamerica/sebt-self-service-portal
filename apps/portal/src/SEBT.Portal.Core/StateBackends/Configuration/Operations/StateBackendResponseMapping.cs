namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>Maps a state backend's raw JSON response into canonical domain types.</summary>
public sealed record StateBackendResponseMapping
{
    /// <summary>Path to the array of records within the raw response (e.g. <c>$.resultSets[0]</c>).</summary>
    public required string Root { get; init; }

    /// <summary>Canonical field name → how to pull and coerce it from the record selected by <see cref="Root"/>.</summary>
    public required Dictionary<string, FieldMapping> Fields { get; init; }

    /// <summary>Optional strategy for grouping records into applications and deciding case inclusion.</summary>
    public StateBackendDisaggregation? Disaggregation { get; init; }

    /// <summary>Optional composition of the case's opaque caseId token; a later write decodes it to recover routing fields.</summary>
    public CaseIdComposition? CaseId { get; init; }
}
