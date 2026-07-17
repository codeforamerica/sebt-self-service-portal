using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheWriteThroughTests
{
    private const string Phone = "3035550199";

    private static GetAccountDetailsResponse Populated(string respCd = "00") => new()
    {
        RespCd = respCd,
        StdntEnrollDtls = new() { new GetAccountStudentDetail { GurdFstNm = "Marker" } }
    };

    [Fact]
    public async Task SetAsync_makes_subsequent_reads_return_set_value()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch();
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch.Delegate);

        var newResponse = Populated("written");
        await sut.SetAsync(Phone, newResponse, CancellationToken.None);

        var read = await sut.GetAsync(Phone, true, CancellationToken.None);

        // Round-trips through JSON, so we get a structurally-equal copy rather than the same instance.
        Assert.NotNull(read);
        Assert.Equal("written", read!.RespCd);
        Assert.Equal("Marker", read.StdntEnrollDtls![0].GurdFstNm);
        Assert.Equal(0, fetch.CallCount);
    }

    [Fact]
    public async Task InvalidateAsync_causes_next_read_to_refetch()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch { NextResponse = Populated() };
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch.Delegate);
        await sut.GetAsync(Phone, true, CancellationToken.None);
        Assert.Equal(1, fetch.CallCount);

        await sut.InvalidateAsync(Phone, CancellationToken.None);
        await sut.GetAsync(Phone, true, CancellationToken.None);

        Assert.Equal(2, fetch.CallCount);
    }

    [Fact]
    public async Task SetAsync_invalidates_shell_entry_so_subsequent_shell_read_refetches()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch { NextResponse = Populated("fetched") };
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch.Delegate);

        await sut.GetAsync(Phone, includeCardService: false, CancellationToken.None);
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:hash:shell", out _));

        await sut.SetAsync(Phone, Populated("written"), CancellationToken.None);

        Assert.False(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:hash:shell", out _));
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:hash:full", out _));

        fetch.NextResponse = Populated("refetched-shell");
        await sut.GetAsync(Phone, includeCardService: false, CancellationToken.None);

        Assert.Equal(2, fetch.CallCount);
    }

    [Fact]
    public async Task SetAsync_tripwire_falls_back_to_invalidate_when_cache_write_throws()
    {
        var hybrid = new ThrowingHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch();
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch.Delegate);

        // Should not throw even though the underlying SetAsync throws.
        await sut.SetAsync(Phone, Populated(), CancellationToken.None);

        Assert.True(hybrid.RemoveCalled);
    }

    private sealed class ThrowingHybridCache : HybridCache
    {
        public bool RemoveCalled { get; private set; }

        public override ValueTask<T> GetOrCreateAsync<TState, T>(
            string key, TState state,
            Func<TState, CancellationToken, ValueTask<T>> factory,
            HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public override ValueTask SetAsync<T>(
            string key, T value, HybridCacheEntryOptions? options = null,
            IEnumerable<string>? tags = null,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated cache write failure");

        public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            RemoveCalled = true;
            return ValueTask.CompletedTask;
        }

        public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
            => ValueTask.CompletedTask;
    }
}
