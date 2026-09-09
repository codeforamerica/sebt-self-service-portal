using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SEBT.Portal.StatePlugins.CO.Cbms.Cache;
using SEBT.Portal.StatePlugins.CO.CbmsApi.Models;
using SEBT.Portal.StatesPlugins.Interfaces.Services;

namespace SEBT.Portal.StatePlugins.CO.Tests.Cbms.Cache;

public class CbmsHouseholdCacheBackgroundRefreshTests
{
    private const string Phone = "3035550199";

    private static GetAccountDetailsResponse Populated(string marker = "v1") => new()
    {
        StdntEnrollDtls = new() { new GetAccountStudentDetail { GurdFstNm = marker } }
    };

    [Fact]
    public async Task Stale_read_returns_cached_value_immediately()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch { NextResponse = Populated("v1") };
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions { SoftExpirationMinutes = 1, HardExpirationMinutes = 240 }),
            fetch.Delegate);

        // Prime with v1, then forcibly stale the envelope.
        await sut.SetAsync(Phone, Populated("v1"), CancellationToken.None);
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:hash:full", out var envelope));
        Assert.NotNull(envelope);
        var staleEnvelope = envelope! with
        {
            SoftExpiryUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
            HardExpiryUtc = DateTimeOffset.UtcNow.AddHours(2)
        };
        await hybrid.SetAsync("co:cbms:hash:full", staleEnvelope);

        fetch.NextResponse = Populated("v2");
        fetch.CallCount = 0;
        var result = await sut.GetAsync(Phone, true, CancellationToken.None);

        // Stale read returns v1 immediately
        Assert.NotNull(result);
        Assert.Equal("v1", result!.StdntEnrollDtls![0].GurdFstNm);
    }

    [Fact]
    public async Task Stale_read_triggers_background_refresh()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var fetch = new FakeFetch();
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions { SoftExpirationMinutes = 1, HardExpirationMinutes = 240 }),
            fetch.Delegate);

        await sut.SetAsync(Phone, Populated("v1"), CancellationToken.None);
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:hash:full", out var envelope));
        var staleEnvelope = envelope! with
        {
            SoftExpiryUtc = DateTimeOffset.UtcNow.AddMinutes(-5)
        };
        await hybrid.SetAsync("co:cbms:hash:full", staleEnvelope);

        fetch.NextResponse = Populated("v2");
        fetch.CallCount = 0;

        await sut.GetAsync(Phone, true, CancellationToken.None);

        // Wait briefly for background task to complete.
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (fetch.CallCount == 0 && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Equal(1, fetch.CallCount);
    }

    [Fact]
    public async Task Background_refresh_failure_keeps_stale_envelope()
    {
        var hybrid = new InMemoryHybridCache();
        var hasher = Substitute.For<IHMACSHA256Hasher>();
        hasher.Hash(Arg.Any<string>()).Returns("hash");
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(CancellationToken.None);
        var failingFetch = new FakeFetch { ThrowOnNext = true };
        var sut = new CbmsHouseholdCache(
            hybrid, hasher, lifetime, NullLoggerFactory.Instance,
            Options.Create(new CbmsHouseholdCacheOptions { SoftExpirationMinutes = 1, HardExpirationMinutes = 240 }),
            failingFetch.Delegate);

        await sut.SetAsync(Phone, Populated("v1"), CancellationToken.None);
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:hash:full", out var envelope));
        var staleEnvelope = envelope! with { SoftExpiryUtc = DateTimeOffset.UtcNow.AddMinutes(-5) };
        await hybrid.SetAsync("co:cbms:hash:full", staleEnvelope);

        failingFetch.CallCount = 0;
        var result = await sut.GetAsync(Phone, true, CancellationToken.None);

        // Stale value still returned synchronously.
        Assert.Equal("v1", result!.StdntEnrollDtls![0].GurdFstNm);

        await Task.Delay(100); // let background refresh attempt happen

        // Cache still has v1 (refresh failed).
        Assert.True(hybrid.TryGet<CbmsHouseholdCacheEnvelope?>("co:cbms:hash:full", out var stillThere));
        Assert.NotNull(stillThere);
        var stillThereResponse = JsonSerializer.Deserialize<GetAccountDetailsResponse>(stillThere!.ResponseJson);
        Assert.Equal("v1", stillThereResponse!.StdntEnrollDtls![0].GurdFstNm);
    }
}
