using SEBT.Portal.Core.Services;

namespace SEBT.Portal.Api.Services;

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
