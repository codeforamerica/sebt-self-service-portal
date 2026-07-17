using SEBT.Portal.Api.Services;

namespace SEBT.Portal.Tests.Unit.Api.Services;

/// <summary>
/// Integration tests for <see cref="PreAuthSessionStore"/> against a real Redis instance.
/// Two stores share the same Redis L2 cache and distributed lock provider, simulating
/// two ECS container replicas coordinating through a shared backing store.
/// </summary>
[Collection("Redis")]
[Trait("Category", "Integration")]
public class RedisPreAuthSessionStoreTests(RedisPreAuthSessionStoreFixture fixture)
{
    [Fact]
    public async Task Get_ReturnsSession_WhenCreatedByDifferentContainerInstance()
    {
        // Container A creates the session; Container B (separate L1 cache) must find it via Redis L2.
        var instanceA = fixture.CreateInstance();
        var instanceB = fixture.CreateInstance();
        await using (instanceA.ServiceProvider)
        await using (instanceB.ServiceProvider)
        {
            var session = await instanceA.Store.CreateAsync("co", "s", "v", "https://r", false);

            var retrieved = await instanceB.Store.GetAsync(session.Id);

            Assert.NotNull(retrieved);
            Assert.Equal(session.Id, retrieved.Id);
            Assert.Equal(session.State, retrieved.State);
        }
    }

    [Fact]
    public async Task TryAdvanceToCallbackCompleted_OnlyOneSucceeds_WhenCalledConcurrentlyAcrossInstances()
    {
        // Two replicas race to advance the same session. The Redis-backed distributed lock
        // must serialize the read-modify-write so exactly one succeeds.
        var instanceA = fixture.CreateInstance();
        var instanceB = fixture.CreateInstance();
        await using (instanceA.ServiceProvider)
        await using (instanceB.ServiceProvider)
        {
            var session = await instanceA.Store.CreateAsync("co", "s", "v", "https://r", false);

            var results = await Task.WhenAll(
                instanceA.Store.TryAdvanceToCallbackCompletedAsync(session.Id, "hash-a"),
                instanceB.Store.TryAdvanceToCallbackCompletedAsync(session.Id, "hash-b"));

            Assert.Equal(1, results.Count(r => r));
            Assert.Equal(1, results.Count(r => !r));
        }
    }
}
