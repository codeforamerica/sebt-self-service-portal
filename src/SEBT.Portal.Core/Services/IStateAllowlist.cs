namespace SEBT.Portal.Core.Services;

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
