using Medallion.Threading;
using Medallion.Threading.Redis;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SEBT.Portal.Api.Services;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace SEBT.Portal.Tests.Unit.Api.Services;

/// <summary>
/// Spins up a Redis container once for the collection. Each call to
/// <see cref="CreateInstance"/> returns a store backed by a fresh in-memory L1
/// cache but sharing the same Redis L2 and <see cref="IDistributedLockProvider"/>
/// — accurately simulating two separate container replicas.
/// </summary>
public sealed class RedisPreAuthSessionStoreFixture : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder("redis:7-alpine").Build();
    private ConnectionMultiplexer _multiplexer = null!;

    public IDistributedLockProvider LockProvider { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _multiplexer = await ConnectionMultiplexer.ConnectAsync(_container.GetConnectionString());
        LockProvider = new RedisDistributedSynchronizationProvider(_multiplexer.GetDatabase());
    }

    public async Task DisposeAsync()
    {
        await _multiplexer.CloseAsync();
        await _container.DisposeAsync();
    }

    public (ServiceProvider ServiceProvider, PreAuthSessionStore Store) CreateInstance()
    {
        var services = new ServiceCollection();
        // Each instance gets its own multiplexer so disposing the ServiceProvider
        // doesn't tear down the shared lock-provider connection.
        services.AddStackExchangeRedisCache(options =>
            options.Configuration = _container.GetConnectionString());
        var sp = services.BuildServiceProvider();
        var cache = sp.GetRequiredService<IDistributedCache>();
        var store = new PreAuthSessionStore(cache, LockProvider, NullLogger<PreAuthSessionStore>.Instance);
        return (sp, store);
    }
}

[CollectionDefinition("Redis")]
public class RedisCollection : ICollectionFixture<RedisPreAuthSessionStoreFixture>
{
}
