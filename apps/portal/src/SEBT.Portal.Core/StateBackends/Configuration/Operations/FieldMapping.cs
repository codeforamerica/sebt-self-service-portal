namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// Maps a single canonical domain field to one source property on a state backend record.
/// A CLOSED, capped primitive (DC-568 spike): the only supported "bricks" are a source
/// property name, an optional exact date format, and an optional named enum table.
///
/// Coercion is driven by the canonical field's known C# type via the mapper's closed setter
/// map — NOT by an explicit coerce kind here. A string field copies; a date field parses with
/// <see cref="Format"/>; a numeric/bool field parses invariant; an enum field resolves via the
/// named <see cref="Enum"/> table.
/// </summary>
public sealed record FieldMapping
{
    /// <summary>Source property name on the record selected by the response mapping's root.</summary>
    public required string From { get; init; }

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
}
