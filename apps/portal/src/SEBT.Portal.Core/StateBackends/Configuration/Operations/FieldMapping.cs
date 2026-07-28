namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Maps a single canonical domain field to one (or, for the keyword-rules brick, one-or-more)
/// source properties on a state backend record.
///
/// A CLOSED, capped set of "bricks" (DC-568 spike): a source property name (<see cref="From"/>),
/// an optional exact date <see cref="Format"/>, an optional named <see cref="Enum"/> table, and an
/// optional <see cref="KeywordRules"/> brick for contains-match inference over free text.
///
/// Coercion is driven by the canonical field's known C# type via the mapper's closed setter map —
/// NOT by an explicit coerce kind here. A string field copies; a date field parses with
/// <see cref="Format"/>; a numeric/bool field parses invariant; an enum field resolves via the
/// named <see cref="Enum"/> table OR, when present, the <see cref="KeywordRules"/> brick.
/// </summary>
public sealed record FieldMapping
{
    /// <summary>
    /// Source property name(s) on the record selected by the response mapping's root. A scalar
    /// (single source) for most fields; a keyword-rules field may list more than one source, in
    /// which case a keyword found in ANY listed source counts.
    /// </summary>
    public required FieldSources From { get; init; }

    /// <summary>
    /// Exact date/time parse format for date-typed canonical fields (e.g. <c>MM/dd/yyyy</c>).
    /// Parsing is exact with this single format — no fallback or transposition.
    /// </summary>
    public string? Format { get; init; }

    /// <summary>
    /// Name of a state-level enum table (see <see cref="StateBackendConfiguration.Enums"/>)
    /// used to translate the source token into a canonical enum value.
    /// </summary>
    public string? Enum { get; init; }

    /// <summary>
    /// Optional contains-match inference brick for an enum-typed field: classify a canonical value
    /// by scanning the <see cref="From"/> source(s) for keyword substrings, first-match-wins.
    /// </summary>
    public KeywordRules? KeywordRules { get; init; }
}
