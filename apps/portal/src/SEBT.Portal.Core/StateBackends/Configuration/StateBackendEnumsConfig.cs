namespace SEBT.Portal.Core.StateBackends.Configuration;

/// <summary>
/// A domain-centered enum translation table. Keyed by OUR canonical enum value (a real C# enum
/// member name), mapping to the one-or-more state tokens that mean it, plus a default for tokens
/// the table does not list.
///
/// Domain-centered on purpose: config authors think in our vocabulary, and inversion to a
/// token → our-value lookup happens at load time. The default applies ONLY to genuinely-unlisted
/// tokens — a token that maps to a mistyped canonical value fails loud at load rather than silently
/// falling through to the default.
/// </summary>
public sealed record StateBackendEnumTable
{
    /// <summary>OUR canonical enum value → the state token(s) that mean it.</summary>
    public required Dictionary<string, List<string>> Map { get; init; }

    /// <summary>Canonical enum value used for tokens not listed in <see cref="Map"/>.</summary>
    public string? Default { get; init; }
}
