using Medallion.Threading;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using SEBT.Portal.Api.Extensions;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.Models.Auth;
using SEBT.Portal.Core.Services;
using SEBT.Portal.Tests.Helpers;

namespace SEBT.Portal.Tests.Unit.Services;

/// <summary>
/// exercises the pre-auth session lifecycle against a real in-memory
/// <see cref="IDistributedCache"/> (no Redis needed). Validates the state machine
/// transitions that protect against replay and session confusion.
/// </summary>
public class PreAuthSessionStoreTests : IDisposable
{
    private readonly ServiceProvider _serviceProvider;
    private readonly PreAuthSessionStore _store;

    public PreAuthSessionStoreTests()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        _serviceProvider = services.BuildServiceProvider();
        var cache = _serviceProvider.GetRequiredService<IDistributedCache>();

        var mockLock = Substitute.For<IDistributedLock>();
        mockLock.AcquireAsync(Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<IDistributedSynchronizationHandle>());
        var lockProvider = Substitute.For<IDistributedLockProvider>();
        lockProvider.CreateLock(Arg.Any<string>()).Returns(mockLock);

        _store = new PreAuthSessionStore(cache, lockProvider, NullLogger<PreAuthSessionStore>.Instance);
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
    public async Task Get_ReturnsSession_WhenSessionExistsInCacheButNotCreatedByThisInstance()
    {
        // Regression test for DC-504: KnownSessionIds was an in-memory gate that
        // short-circuited cache lookups for IDs not created by the current process.
        // On a multi-container deployment, sessions created on Container A never
        // appeared in Container B's KnownSessionIds, so every cross-instance lookup
        // returned null (missing_session) even though the session was in Redis.
        //
        // This test simulates that by seeding the distributed cache directly,
        // bypassing CreateAsync (the only path that previously populated
        // KnownSessionIds on the current process).
        var cache = _serviceProvider.GetRequiredService<IDistributedCache>();
        var sessionId = "simulated-remote-session";
        var session = new PreAuthSession
        {
            Id = sessionId,
            State = "remote-state",
            CodeVerifier = "remote-verifier",
            StateCode = "co",
            RedirectUri = "https://example.com/callback",
            Phase = PreAuthSessionPhase.Created
        };

        await cache.SetAsync(
            PreAuthSessionStore.CacheKeyPrefix + sessionId,
            session,
            new DistributedCacheEntryOptions());

        var retrieved = await _store.GetAsync(sessionId);

        Assert.NotNull(retrieved);
        Assert.Equal(sessionId, retrieved.Id);
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

    [Fact]
    public async Task TryAdvanceToCallbackCompleted_OnlyOneSucceeds_WhenCalledConcurrentlyAcrossInstances()
    {
        // Two stores share the same backing cache and a real in-process lock provider
        // (not a mock), simulating two container replicas coordinating via a shared lock.
        // The lock must serialize the read-modify-write: whichever store acquires first
        // advances the phase to CallbackCompleted; the second reads that updated phase
        // and returns false.
        var cache = _serviceProvider.GetRequiredService<IDistributedCache>();
        var lockProvider = new InProcessLockProvider();
        var storeA = new PreAuthSessionStore(cache, lockProvider, NullLogger<PreAuthSessionStore>.Instance);
        var storeB = new PreAuthSessionStore(cache, lockProvider, NullLogger<PreAuthSessionStore>.Instance);

        var session = await storeA.CreateAsync("co", "s", "v", "https://r", false);

        var results = await Task.WhenAll(
            storeA.TryAdvanceToCallbackCompletedAsync(session.Id, "hash-a"),
            storeB.TryAdvanceToCallbackCompletedAsync(session.Id, "hash-b"));

        Assert.Equal(1, results.Count(r => r));
        Assert.Equal(1, results.Count(r => !r));
    }
}

