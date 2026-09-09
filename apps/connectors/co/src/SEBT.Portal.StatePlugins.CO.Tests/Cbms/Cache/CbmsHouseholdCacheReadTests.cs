using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheReadTests
{
    private const string Phone = "3035550199";

    private static GetAccountDetailsResponse Populated() => new()
    {
        StdntEnrollDtls = new() { new GetAccountStudentDetail() }
    };

    private static GetAccountDetailsResponse Empty() => new()
    {
        StdntEnrollDtls = new()
    };

    private static (CbmsHouseholdCache sut, InMemoryHybridCache hybrid, FakeFetch fetch) Build(
        CbmsHouseholdCacheOptions? options = null)
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns(c => "hash:" + c.Arg<string>());
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch();
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(options ?? new CbmsHouseholdCacheOptions()),
            fetch.Delegate);
        return (sut, hybrid, fetch);
    }

    [Fact]
    public async Task Cache_miss_calls_CBMS_then_caches()
    {
        var (sut, _, fetch) = Build();
        fetch.NextResponse = Populated();

        var result = await sut.GetAsync(Phone, true, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(1, fetch.CallCount);
    }

    [Fact]
    public async Task Cache_hit_does_not_call_CBMS_again()
    {
        var (sut, _, fetch) = Build();
        fetch.NextResponse = Populated();
        await sut.GetAsync(Phone, true, CancellationToken.None); // primes
        fetch.CallCount = 0;

        await sut.GetAsync(Phone, true, CancellationToken.None);

        Assert.Equal(0, fetch.CallCount);
    }

    [Fact]
    public async Task Cache_returns_null_when_CBMS_returns_empty_household()
    {
        var (sut, _, fetch) = Build();
        fetch.NextResponse = Empty();

        var result = await sut.GetAsync(Phone, true, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Cache_returns_null_when_CBMS_returns_null()
    {
        var (sut, _, fetch) = Build();
        fetch.NextResponse = null;

        var result = await sut.GetAsync(Phone, true, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task Negative_cache_does_not_immediately_refetch_within_window()
    {
        var (sut, _, fetch) = Build(new CbmsHouseholdCacheOptions { NegativeCacheSeconds = 60 });
        fetch.NextResponse = Empty();
        await sut.GetAsync(Phone, true, CancellationToken.None);
        fetch.CallCount = 0;

        await sut.GetAsync(Phone, true, CancellationToken.None);

        Assert.Equal(0, fetch.CallCount);
    }

    /// <summary>
    /// Regression test for the framework-TTL bug: negative responses must be stored at the
    /// HybridCache framework layer with NegativeCacheExpiration as the TTL — not HardExpiration.
    /// HybridCacheEntryOptions.Expiration governs both L1 and L2 framework eviction; the envelope's
    /// internal SoftExpiryUtc/HardExpiryUtc only gate our own short-circuit logic and do NOT cause
    /// the framework to evict. If the negative entry is stored with HardExpiration (4 hr), CBMS
    /// never gets re-checked for the full hard window after a "household not found" response —
    /// breaking the household-creation race window the negative cache is meant to protect against.
    /// </summary>
    [Fact]
    public async Task Negative_cache_entry_uses_NegativeCacheExpiration_as_framework_TTL()
    {
        var options = new CbmsHouseholdCacheOptions
        {
            SoftExpirationMinutes = 15,
            HardExpirationMinutes = 240,
            NegativeCacheSeconds = 60,
            BackgroundRefreshTimeoutSeconds = 60
        };
        var (sut, hybrid, fetch) = Build(options);
        fetch.NextResponse = Empty();

        var result = await sut.GetAsync(Phone, true, CancellationToken.None);

        Assert.Null(result);
        var key = "co:cbms:hash:" + Phone + ":full";
        var latestOpts = hybrid.LatestOptionsFor(key);
        Assert.NotNull(latestOpts);
        Assert.Equal(options.NegativeCacheExpiration, latestOpts!.Expiration);
    }

    /// <summary>
    /// Regression test for cross-pod L1 staleness bound: positive entries must set
    /// <c>LocalCacheExpiration</c> short enough that other pods' L1 caches eventually fall through
    /// to L2 (Redis) and pick up cross-pod write-throughs. If <c>LocalCacheExpiration</c> is unset,
    /// HybridCache defaults it to <c>Expiration</c> (HardExpiration, 4 hr) — which means a write
    /// on pod A would not be visible on pod B's L1 for hours.
    /// </summary>
    [Fact]
    public async Task Positive_entry_sets_LocalCacheExpiration_to_bound_cross_pod_L1_staleness()
    {
        var options = new CbmsHouseholdCacheOptions
        {
            SoftExpirationMinutes = 15,
            HardExpirationMinutes = 240,
            NegativeCacheSeconds = 60,
            BackgroundRefreshTimeoutSeconds = 60,
            LocalCacheExpirationSeconds = 60
        };
        var (sut, hybrid, fetch) = Build(options);
        fetch.NextResponse = Populated();

        await sut.GetAsync(Phone, true, CancellationToken.None);

        var key = "co:cbms:hash:" + Phone + ":full";
        var latestOpts = hybrid.LatestOptionsFor(key);
        Assert.NotNull(latestOpts);
        Assert.Equal(options.HardExpiration, latestOpts!.Expiration);
        Assert.Equal(options.LocalCacheExpiration, latestOpts.LocalCacheExpiration);
    }

    /// <summary>
    /// Regression test for the cross-instance negative-detection bug: an envelope read from
    /// the cache after L2 (Redis) deserialization will have a freshly-allocated
    /// <see cref="GetAccountDetailsResponse"/> instance, NOT the in-process sentinel singleton.
    /// Detection of "no household" must therefore be value-based (empty rows) rather than
    /// reference-based — otherwise pod B reading what pod A negative-cached would treat the
    /// entry as a positive household with zero rows, returning a non-null response to the caller.
    /// We simulate the cross-instance case by manually pre-populating the cache with an envelope
    /// containing a freshly-serialized empty-response JSON payload.
    /// </summary>
    [Fact]
    public async Task Negative_detection_works_for_envelopes_with_independently_allocated_empty_response()
    {
        var (sut, hybrid, fetch) = Build();
        var key = "co:cbms:hash:" + Phone + ":full";
        var freshEmpty = new GetAccountDetailsResponse { StdntEnrollDtls = new() };
        var now = DateTimeOffset.UtcNow;
        var envelope = new CbmsHouseholdCacheEnvelope(
            ResponseJson: System.Text.Json.JsonSerializer.Serialize(freshEmpty),
            SoftExpiryUtc: now.AddSeconds(60),
            HardExpiryUtc: now.AddSeconds(60),
            CachedAtUtc: now);
        await hybrid.SetAsync(key, envelope);

        // Factory should not be invoked — we have a (negative) cached entry.
        fetch.NextResponse = Populated();

        var result = await sut.GetAsync(Phone, true, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, fetch.CallCount);
    }
}
