using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using SEBT.Portal.Core.AppSettings;

namespace SEBT.Portal.Api.Services;

/// <inheritdoc cref="ITokenDenylist"/>
public sealed class TokenDenylist : ITokenDenylist
{
    internal const string CacheKeyPrefix = "auth:denylist:";

    /// <summary>
    /// Shared clock-skew allowance for portal JWTs: the bearer middleware's
    /// <c>TokenValidationParameters.ClockSkew</c> (Program.cs) and the denylist TTL padding
    /// below both reference this single constant. A token validates for up to this long
    /// past <c>exp</c>, so denylist entries must outlive the token by the same margin — if
    /// the two values diverged, a revoked token could regain a brief usable window.
    /// </summary>
    internal static readonly TimeSpan ClockSkewPadding = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Minimum interval between logged lookup failures. Lookups run on every authenticated
    /// request (the bearer middleware's <c>OnTokenValidated</c>), so a cache outage would
    /// otherwise emit one error log per request — enough to flood log storage. Only the
    /// logging is throttled; every failure still fails open.
    /// </summary>
    internal static readonly TimeSpan FailureLogInterval = TimeSpan.FromMinutes(1);

    private static readonly byte[] DeniedMarker = "1"u8.ToArray();

    private readonly IDistributedCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<TokenDenylist> _logger;
    private readonly IOptions<JwtSettings> _jwtSettingsOptions;

    /// <summary>UTC ticks of the last logged lookup failure; guarded by Interlocked.</summary>
    private long _lastLookupFailureLogTicks;

    /// <inheritdoc cref="TokenDenylist"/>
    public TokenDenylist(
        IDistributedCache cache,
        TimeProvider timeProvider,
        ILogger<TokenDenylist> logger,
        IOptions<JwtSettings> jwtSettingsOptions)
    {
        _cache = cache;
        _timeProvider = timeProvider;
        _logger = logger;
        _jwtSettingsOptions = jwtSettingsOptions;
    }

    /// <inheritdoc/>
    public async Task DenyAsync(string jti, DateTimeOffset tokenExpiresAt, CancellationToken cancellationToken = default)
    {
        var ttl = tokenExpiresAt + ClockSkewPadding - _timeProvider.GetUtcNow();
        if (ttl <= TimeSpan.Zero)
        {
            return;
        }

        // No honestly-minted portal token can outlive ExpirationMinutes, so a computed TTL
        // beyond that (plus skew) can only come from a forged exp claim. Clamp it so a
        // caller-supplied expiry can't plant an arbitrarily long-lived entry in the shared
        // cache — the caller here is untrusted, since logout decodes the cookie JWT without
        // validating it.
        var maxTtl = TimeSpan.FromMinutes(_jwtSettingsOptions.Value.ExpirationMinutes) + ClockSkewPadding;
        if (ttl > maxTtl)
        {
            ttl = maxTtl;
        }

        try
        {
            await _cache.SetAsync(
                CacheKeyPrefix + jti,
                DeniedMarker,
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to denylist token jti; logout proceeds without revocation");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsDeniedAsync(string jti, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _cache.GetAsync(CacheKeyPrefix + jti, cancellationToken) is not null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogLookupFailureThrottled(ex);
            return false;
        }
    }

    private void LogLookupFailureThrottled(Exception ex)
    {
        var now = _timeProvider.GetUtcNow().UtcTicks;
        var last = Interlocked.Read(ref _lastLookupFailureLogTicks);
        if (now - last < FailureLogInterval.Ticks)
        {
            return;
        }

        // Only the thread that wins the exchange logs, so concurrent failures during an
        // outage still produce a single entry per interval.
        if (Interlocked.CompareExchange(ref _lastLookupFailureLogTicks, now, last) == last)
        {
            _logger.LogError(
                ex,
                "Token denylist lookup failed; failing open (repeat failures suppressed for {SuppressionInterval})",
                FailureLogInterval);
        }
    }
}
