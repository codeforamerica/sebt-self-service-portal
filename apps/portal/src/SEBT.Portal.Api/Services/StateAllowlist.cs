namespace SEBT.Portal.Api.Services;

/// <summary>
/// Server-side allowlist of state codes permitted to use the OIDC login endpoints.
/// Derived at startup from per-state appsettings overlays that have <c>Oidc:DiscoveryEndpoint</c>
/// configured; an instance loaded with STATE=co and CO's Oidc block exposes {"co"}, nothing else.
/// Any <c>stateCode</c> in a route or request body that isn't in this set is rejected before
/// the OIDC flow touches PingOne, blocking attempts to use an instance as an unintended tenant.
/// </summary>
public interface IStateAllowlist
{
    /// <summary>True if <paramref name="stateCode"/> (case-insensitive) is a configured OIDC tenant.</summary>
    bool Contains(string? stateCode);

    /// <summary>
    /// Resolves a user-provided stateCode to the canonical (lowercased) value from the
    /// allowlist. Returns null if not found. The returned value is from the allowlist itself,
    /// not derived from user input — safe for logging and downstream use without taint.
    /// </summary>
    string? TryResolve(string? stateCode);

    /// <summary>Lowercased, case-insensitive set of allowed state codes.</summary>
    IReadOnlySet<string> All { get; }
}

/// <inheritdoc cref="IStateAllowlist"/>
public sealed class StateAllowlist : IStateAllowlist
{
    private readonly HashSet<string> _states;

    /// <summary>Builds an allowlist from a lowercased, de-duplicated view of <paramref name="states"/>.</summary>
    public StateAllowlist(IEnumerable<string> states)
    {
        _states = new HashSet<string>(
            states.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public bool Contains(string? stateCode) =>
        !string.IsNullOrWhiteSpace(stateCode) && _states.Contains(stateCode.ToLowerInvariant());

    /// <inheritdoc/>
    public string? TryResolve(string? stateCode)
    {
        if (string.IsNullOrWhiteSpace(stateCode)) return null;
        var normalized = stateCode.ToLowerInvariant();
        // Return the canonical value from the set, breaking any taint chain from user input.
        return _states.TryGetValue(normalized, out var canonical) ? canonical : null;
    }

    /// <inheritdoc/>
    public IReadOnlySet<string> All => _states;
}
