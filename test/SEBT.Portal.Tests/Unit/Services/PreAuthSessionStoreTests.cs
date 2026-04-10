using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SEBT.Portal.Api.Services;

namespace SEBT.Portal.Tests.Unit.Services;

/// <summary>
/// exercises the pre-auth session lifecycle against a real in-memory
/// <see cref="HybridCache"/> (no Redis needed). Validates the state machine
/// transitions that protect against replay and session confusion.
/// </summary>
public class PreAuthSessionStoreTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PreAuthSessionStore _store;

    public PreAuthSessionStoreTests()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        services.AddMemoryCache();
        _serviceProvider = services.BuildServiceProvider();
        var cache = _serviceProvider.GetRequiredService<HybridCache>();
        _store = new PreAuthSessionStore(cache, NullLogger<PreAuthSessionStore>.Instance);
    }

    public void Dispose() => _serviceProvider.Dispose();

    [Fact]
    public async Task Create_ReturnsSessionWithGeneratedId()
    {
        var session = await _store.CreateAsync("co", "state1", "verifier1", "https://app/cb", false);

        Assert.NotNull(session);
        Assert.NotEmpty(session.Id);
        Assert.Equal("co", session.StateCode);
        Assert.Equal("state1", session.State);
        Assert.Equal("verifier1", session.CodeVerifier);
        Assert.Equal(PreAuthSessionPhase.Created, session.Phase);
    }

    [Fact]
    public async Task Get_ReturnsCreatedSession()
    {
        var session = await _store.CreateAsync("co", "s", "v", "https://r", false);

        var retrieved = await _store.GetAsync(session.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(session.Id, retrieved.Id);
        Assert.Equal(session.State, retrieved.State);
    }

    [Fact]
    public async Task Get_ReturnsNullForUnknownId()
    {
        var result = await _store.GetAsync("nonexistent-id");

        Assert.Null(result);
    }

    [Fact]
    public async Task TryAdvanceToCallbackCompleted_SucceedsFromCreated()
    {
        var session = await _store.CreateAsync("co", "s", "v", "https://r", false);

        var result = await _store.TryAdvanceToCallbackCompletedAsync(session.Id, "hash1");

        Assert.True(result);
        var updated = await _store.GetAsync(session.Id);
        Assert.Equal(PreAuthSessionPhase.CallbackCompleted, updated!.Phase);
        Assert.Equal("hash1", updated.CallbackTokenHash);
    }

    [Fact]
    public async Task TryAdvanceToCallbackCompleted_FailsFromCallbackCompleted()
    {
        var session = await _store.CreateAsync("co", "s", "v", "https://r", false);
        await _store.TryAdvanceToCallbackCompletedAsync(session.Id, "hash1");

        var result = await _store.TryAdvanceToCallbackCompletedAsync(session.Id, "hash2");

        Assert.False(result);
    }

    [Fact]
    public async Task TryAdvanceToCallbackCompleted_FailsForUnknownSession()
    {
        var result = await _store.TryAdvanceToCallbackCompletedAsync("nonexistent", "hash");

        Assert.False(result);
    }

    [Fact]
    public async Task TryAdvanceToLoginCompleted_SucceedsFromCallbackCompleted()
    {
        var session = await _store.CreateAsync("co", "s", "v", "https://r", false);
        await _store.TryAdvanceToCallbackCompletedAsync(session.Id, "hash1");

        var result = await _store.TryAdvanceToLoginCompletedAsync(session.Id, "hash1");

        Assert.True(result);
        var updated = await _store.GetAsync(session.Id);
        Assert.Equal(PreAuthSessionPhase.LoginCompleted, updated!.Phase);
    }

    [Fact]
    public async Task TryAdvanceToLoginCompleted_FailsWhenTokenHashMismatch()
    {
        var session = await _store.CreateAsync("co", "s", "v", "https://r", false);
        await _store.TryAdvanceToCallbackCompletedAsync(session.Id, "hash1");

        var result = await _store.TryAdvanceToLoginCompletedAsync(session.Id, "wrong-hash");

        Assert.False(result);
    }

    [Fact]
    public async Task TryAdvanceToLoginCompleted_FailsFromCreated()
    {
        var session = await _store.CreateAsync("co", "s", "v", "https://r", false);

        var result = await _store.TryAdvanceToLoginCompletedAsync(session.Id, "hash1");

        Assert.False(result);
    }

    [Fact]
    public async Task TryAdvanceToLoginCompleted_FailsOnSecondAttempt_Replay()
    {
        var session = await _store.CreateAsync("co", "s", "v", "https://r", false);
        await _store.TryAdvanceToCallbackCompletedAsync(session.Id, "hash1");
        await _store.TryAdvanceToLoginCompletedAsync(session.Id, "hash1");

        // Replay: try to use the same session again
        var result = await _store.TryAdvanceToLoginCompletedAsync(session.Id, "hash1");

        Assert.False(result);
    }

    [Fact]
    public async Task Remove_MakesSessionUnretrievable()
    {
        var session = await _store.CreateAsync("co", "s", "v", "https://r", false);

        await _store.RemoveAsync(session.Id);
        var result = await _store.GetAsync(session.Id);

        Assert.Null(result);
    }

    [Fact]
    public void HashCallbackToken_ProducesConsistentHash()
    {
        var hash1 = IPreAuthSessionStore.HashCallbackToken("test-token");
        var hash2 = IPreAuthSessionStore.HashCallbackToken("test-token");

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // SHA-256 hex = 64 chars
    }

    [Fact]
    public void HashCallbackToken_DifferentTokensProduceDifferentHashes()
    {
        var hash1 = IPreAuthSessionStore.HashCallbackToken("token-a");
        var hash2 = IPreAuthSessionStore.HashCallbackToken("token-b");

        Assert.NotEqual(hash1, hash2);
    }
}
