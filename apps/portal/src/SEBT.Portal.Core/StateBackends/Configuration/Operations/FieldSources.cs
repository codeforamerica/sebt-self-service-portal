using System.Collections;

namespace SEBT.Portal.Core.StateBackends.Configuration.Operations;

/// <summary>
/// One-or-more source property names for a <see cref="FieldMapping.From"/>. Most fields bind a
/// single source; the keyword-rules brick may scan several. Modeled as a small value type so a
/// scalar YAML <c>from: X</c> and a sequence <c>from: [X, Y]</c> both hydrate, and so a scalar
/// still behaves like a bare string at single-source call sites (implicit conversions below).
/// </summary>
public sealed class FieldSources : IEnumerable<string>
{
    private readonly IReadOnlyList<string> _sources;

    public FieldSources(IReadOnlyList<string> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        if (sources.Count == 0)
        {
            throw new ArgumentException("A field mapping must name at least one source.", nameof(sources));
        }

        _sources = sources;
    }

    /// <summary>All source property names, in declared order.</summary>
    public IReadOnlyList<string> All => _sources;

    /// <summary>
    /// The single source. Throws when the mapping names more than one — a scalar-only call site
    /// (e.g. a string/date/enum field) must not be handed a multi-source keyword-rules field.
    /// </summary>
    public string Single => _sources.Count == 1
        ? _sources[0]
        : throw new InvalidOperationException(
            $"Field mapping names {_sources.Count} sources; expected exactly one.");

    public static implicit operator FieldSources(string source) => new(new[] { source });

    public static implicit operator FieldSources(string[] sources) => new(sources);

    // Lets a single-source FieldSources stand in for a plain source name. Null-tolerant so a
    // null-conditional access (e.g. `mapping?.From`) still converts without a nullable warning.
    public static implicit operator string?(FieldSources? sources) => sources?.Single;

    public IEnumerator<string> GetEnumerator() => _sources.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
