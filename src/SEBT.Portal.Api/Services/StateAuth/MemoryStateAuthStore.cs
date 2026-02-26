using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using SEBT.Portal.StatesPlugins.Interfaces;
using SEBT.Portal.StatesPlugins.Interfaces.Models;

namespace SEBT.Portal.Api.Services.StateAuth;

/// <summary>
/// In-memory store for state IdP auth context (e.g. after OIDC callback).
/// The host resolves this and <see cref="IStateAuthSessionAccessor"/> per request and passes context into <see cref="IStateAuthService"/> plugin methods.
/// </summary>
internal sealed class MemoryStateAuthStore : IStateAuthStore
{
    private readonly ConcurrentDictionary<string, (StateAuthContext Context, DateTimeOffset ExpiresAt)> _store = new();
    private readonly ILogger<MemoryStateAuthStore>? _logger;

    public MemoryStateAuthStore(ILogger<MemoryStateAuthStore>? logger = null)
    {
        _logger = logger;
    }

    public Task SetAsync(
        string sessionId,
        StateAuthContext context,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(expiration);
        _store[sessionId] = (context, expiresAt);
        return Task.CompletedTask;
    }

    public Task<StateAuthContext?> GetAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(sessionId, out var entry))
            return Task.FromResult<StateAuthContext?>(null);
        if (DateTimeOffset.UtcNow >= entry.ExpiresAt)
        {
            _store.TryRemove(sessionId, out _);
            return Task.FromResult<StateAuthContext?>(null);
        }
        return Task.FromResult<StateAuthContext?>(entry.Context);
    }
}
