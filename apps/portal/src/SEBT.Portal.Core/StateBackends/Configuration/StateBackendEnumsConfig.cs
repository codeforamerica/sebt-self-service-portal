namespace SEBT.Portal.Core.StateBackends.Configuration;

/// <summary>An enum translation table; <see cref="Default"/> applies only to unlisted tokens — a mistyped canonical value fails at load.</summary>
public sealed record StateBackendEnumTable
{
    /// <summary>Our canonical enum value → the state token(s) that mean it.</summary>
    public required Dictionary<string, List<string>> Map { get; init; }

    /// <summary>Canonical enum value used for tokens not listed in <see cref="Map"/>.</summary>
    public string? Default { get; init; }
}
