using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheKeyHashingTests
{
    private const string Phone = "3035550199";

    [Fact]
    public async Task GetAsync_uses_co_cbms_namespace_with_hashed_phone()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Phone).Returns("deadbeef");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);

        CbmsFetchAccountDetailsDelegate fetch = (_, _, _) => Task.FromResult<GetAccountDetailsResponse?>(
            new() { StdntEnrollDtls = new() { new GetAccountStudentDetail() } });

        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch);

        await sut.GetAsync(Phone, true, CancellationToken.None);

        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:live:deadbeef:full", out _));
    }

    [Fact]
    public async Task GetAsync_invokes_hasher_with_normalized_phone()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);

        CbmsFetchAccountDetailsDelegate fetch = (_, _, _) => Task.FromResult<GetAccountDetailsResponse?>(
            new() { StdntEnrollDtls = new() { new GetAccountStudentDetail() } });
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch);

        await sut.GetAsync(Phone, true, CancellationToken.None);

        hasher.Received(1).Hash(Phone);
    }

    [Fact]
    public async Task Cache_key_never_contains_raw_phone()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Phone).Returns("nothingmatchingaphone");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);

        CbmsFetchAccountDetailsDelegate fetch = (_, _, _) => Task.FromResult<GetAccountDetailsResponse?>(
            new() { StdntEnrollDtls = new() { new GetAccountStudentDetail() } });
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch);

        await sut.GetAsync(Phone, true, CancellationToken.None);

        Assert.DoesNotContain(hybrid.Keys, k => k.Contains(Phone));
    }

    [Fact]
    public async Task GetAsync_uses_distinct_cache_keys_for_shell_vs_full_payload()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Phone).Returns("abc123");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);

        var fetchCount = 0;
        CbmsFetchAccountDetailsDelegate fetch = (_, includeCards, _) =>
        {
            fetchCount++;
            return Task.FromResult<GetAccountDetailsResponse?>(
                new()
                {
                    StdntEnrollDtls = new()
                    {
                        new GetAccountStudentDetail
                        {
                            EbtCardLastFour = includeCards ? "1234" : null
                        }
                    }
                });
        };

        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch);

        await sut.GetAsync(Phone, includeCardService: false, CancellationToken.None);
        await sut.GetAsync(Phone, includeCardService: true, CancellationToken.None);

        Assert.Equal(2, fetchCount);
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:live:abc123:shell", out _));
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:live:abc123:full", out _));
    }

    [Fact]
    public async Task GetAsync_stores_mock_and_live_responses_under_separate_keys()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Phone).Returns("deadbeef");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);

        var liveFetches = 0;
        var mockFetches = 0;
        CbmsFetchAccountDetailsDelegate liveFetch = (_, _, _) =>
        {
            liveFetches++;
            return Task.FromResult<GetAccountDetailsResponse?>(
                new() { StdntEnrollDtls = new() { new GetAccountStudentDetail() } });
        };
        CbmsFetchAccountDetailsDelegate mockFetch = (_, _, _) =>
        {
            mockFetches++;
            return Task.FromResult<GetAccountDetailsResponse?>(
                new() { StdntEnrollDtls = new() { new GetAccountStudentDetail() } });
        };

        var live = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            liveFetch);
        var mock = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions()),
            mockFetch,
            useMockResponses: true);

        await live.GetAsync(Phone, true, CancellationToken.None);
        await mock.GetAsync(Phone, true, CancellationToken.None);
        await mock.GetAsync(Phone, true, CancellationToken.None);

        Assert.Equal(1, liveFetches);
        Assert.Equal(1, mockFetches);
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:live:deadbeef:full", out _));
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:mock:deadbeef:full", out _));
    }

    [Fact]
    public async Task GetAsync_when_built_for_mock_responses_logs_warning()
    {
        var logger = Substitute.For<ILogger>();
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(logger);

        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Phone).Returns("deadbeef");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        CbmsFetchAccountDetailsDelegate fetch = (_, _, _) => Task.FromResult<GetAccountDetailsResponse?>(
            new() { StdntEnrollDtls = new() { new GetAccountStudentDetail() } });

        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, loggerFactory,
            Options.Create(new CbmsHouseholdCacheOptions()),
            fetch,
            useMockResponses: true);

        await sut.GetAsync(Phone, true, CancellationToken.None);

        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Is<object>(state => state.ToString()!.Contains("served from mock responses")),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());
    }
}
