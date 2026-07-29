namespace SEBT.Portal.Core.StateBackends.Configuration;

/// <summary>
/// An enum translation table keyed by our canonical enum value, mapping to the state token(s) that
/// mean it. <see cref="Default"/> applies only to genuinely-unlisted tokens; a token mapped to a
/// mistyped canonical value fails loud at load rather than falling through to the default.
/// </summary>
public sealed record StateBackendEnumTable
{
    /// <summary>Our canonical enum value → the state token(s) that mean it.</summary>
    public required Dictionary<string, List<string>> Map { get; init; }

    /// <summary>Canonical enum value used for tokens not listed in <see cref="Map"/>.</summary>
    public string? Default { get; init; }
}
