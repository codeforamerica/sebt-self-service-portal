namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Maps one canonical domain field to source propert(ies) on a state backend record; coercion is
/// driven by the canonical field's C# type, not an explicit kind here.
/// </summary>
public sealed record FieldMapping
{
    /// <summary>Source property name(s) on the selected record.</summary>
    public required FieldSources From { get; init; }

    /// <summary>Exact date/time parse format for date-typed fields (e.g. <c>MM/dd/yyyy</c>); no fallback.</summary>
    public string? Format { get; init; }

    /// <summary>Name of a <see cref="StateBackendConfiguration.Enums"/> table translating the source token to a canonical enum value.</summary>
    public string? Enum { get; init; }

    /// <summary>Contains-match inference over the <see cref="From"/> source(s) for an enum-typed field.</summary>
    public KeywordRules? KeywordRules { get; init; }
}
