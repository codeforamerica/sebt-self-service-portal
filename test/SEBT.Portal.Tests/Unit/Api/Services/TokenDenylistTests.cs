using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SEBT.Portal.Api.Services;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Tests.Unit.Api.Services;

public class TokenDenylistTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Builds a TokenDenylist with the given backing cache and clock, and a JwtSettings
    /// whose ExpirationMinutes defaults to the portal's real 15-minute idle session
    /// lifetime — the ceiling that DenyAsync clamps forged/oversized expiries against.
    /// </summary>
    private static TokenDenylist CreateDenylist(
        IDistributedCache cache,
        TimeProvider timeProvider,
        int expirationMinutes = 15) =>
        new(cache, timeProvider, NullLogger<TokenDenylist>.Instance, Options.Create(new JwtSettings
        {
            SecretKey = new string('x', 32),
            Issuer = "test",
            Audience = "test",
            ExpirationMinutes = expirationMinutes
        }));

    private static TokenDenylist CreateWithMemoryCache(out IDistributedCache cache)
    {
        cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        return CreateDenylist(cache, new FakeTimeProvider(Now));
    }

    [Fact]
    public async Task IsDeniedAsync_AfterDenyAsync_ReturnsTrue()
    {
        var denylist = CreateWithMemoryCache(out _);

        await denylist.DenyAsync("jti-a", Now.AddMinutes(15));

        Assert.True(await denylist.IsDeniedAsync("jti-a"));
    }

    [Fact]
    public async Task IsDeniedAsync_ForUnknownJti_ReturnsFalse()
    {
        var denylist = CreateWithMemoryCache(out _);

        await denylist.DenyAsync("jti-a", Now.AddMinutes(15));

        Assert.False(await denylist.IsDeniedAsync("jti-b"));
    }

    [Fact]
    public async Task DenyAsync_SetsTtlToRemainingLifetimePlusClockSkew()
    {
        var cache = Substitute.For<IDistributedCache>();
        var denylist = CreateDenylist(cache, new FakeTimeProvider(Now));

        // 5 minutes remaining sits below the clamp ceiling, so this pins the unclamped
        // formula — an always-clamping regression would fail here.
        await denylist.DenyAsync("jti-a", Now.AddMinutes(5));

        await cache.Received(1).SetAsync(
            "auth:denylist:jti-a",
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o =>
                o.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(5) + TokenDenylist.ClockSkewPadding),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DenyAsync_ForTokenPastTheSkewWindow_DoesNotWrite()
    {
        var cache = Substitute.For<IDistributedCache>();
        var denylist = CreateDenylist(cache, new FakeTimeProvider(Now));

        await denylist.DenyAsync("jti-a", Now - TokenDenylist.ClockSkewPadding);

        await cache.DidNotReceiveWithAnyArgs().SetAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task DenyAsync_WhenCacheThrows_DoesNotThrow()
    {
        var cache = Substitute.For<IDistributedCache>();
        cache.SetAsync(default!, default!, default!, default)
            .ThrowsAsyncForAnyArgs(new InvalidOperationException("redis down"));
        var denylist = CreateDenylist(cache, new FakeTimeProvider(Now));

        await denylist.DenyAsync("jti-a", Now.AddMinutes(15));
    }

    [Fact]
    public async Task IsDeniedAsync_WhenCacheThrows_FailsOpen()
    {
        var cache = Substitute.For<IDistributedCache>();
        cache.GetAsync(default!, default).ThrowsAsyncForAnyArgs(new InvalidOperationException("redis down"));
        var denylist = CreateDenylist(cache, new FakeTimeProvider(Now));

        Assert.False(await denylist.IsDeniedAsync("jti-a"));
    }

    [Fact]
    public async Task DenyAsync_ForForgedFarFutureExpiry_ClampsTtlToJwtSettingsCeiling()
    {
        // An unauthenticated caller controls the cookie JWT's exp claim at logout (it is
        // decoded without validation). A forged year-9999 exp must not translate into a
        // multi-decade cache entry — the real ceiling is the portal's own token lifetime.
        var cache = Substitute.For<IDistributedCache>();
        var denylist = CreateDenylist(cache, new FakeTimeProvider(Now), expirationMinutes: 15);

        await denylist.DenyAsync("jti-a", Now.AddYears(100));

        await cache.Received(1).SetAsync(
            "auth:denylist:jti-a",
            Arg.Any<byte[]>(),
            Arg.Is<DistributedCacheEntryOptions>(o =>
                o.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(15) + TokenDenylist.ClockSkewPadding),
            Arg.Any<CancellationToken>());
    }
}
