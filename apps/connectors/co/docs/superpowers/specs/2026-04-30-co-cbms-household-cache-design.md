# Design: Colorado CBMS Household Response Cache

**Date:** 2026-04-30
**Repo:** sebt-self-service-portal-co-connector (encapsulated; no main-portal or state-connector contract changes)
**Branch:** feature/co-cbms-household-cache
**Status:** Draft
**ADR:** [0004-cbms-response-caching.md](../../adr/0004-cbms-response-caching.md)

## Purpose

Eliminate user-visible latency from the Colorado CBMS `get-account-details` endpoint by introducing a cache-aside layer with stale-while-revalidate (SWR) semantics and write-through on successful PATCH operations. The cache is encapsulated entirely inside the CO plugin and shared across the three Colorado services that read household data.

## Background

CBMS `get-account-details` averages 8-9 seconds per call and has been observed at 30+ seconds. The companion PATCH endpoint (`update-std-dtls`) is fast — averaging under 1 second. The latency problem is the read leg, not the write leg. Three Colorado services hit `get-account-details`:

- `ColoradoSummerEbtCaseService.GetHouseholdByPhoneAsync` — read path (display)
- `ColoradoAddressUpdateService.UpdateAddressAsync` — slow read (to obtain CBMS internal IDs) followed by fast PATCH `update-std-dtls`
- `ColoradoCardReplacementService.RequestCardReplacementAsync` — slow read (to obtain CBMS internal IDs) followed by fast PATCH `update-std-dtls`

Because the write services need raw CBMS IDs (`sebtChldId`, `sebtAppId`, `sebtChldCwin`) that are not carried on the canonical portal `HouseholdData` model, today's write flow makes a slow `get-account-details` call before each PATCH. A typical "view → submit address change" flow makes three CBMS calls in total — two slow reads (display + write-handler re-read) plus a fast PATCH — roughly 17-19 s under average conditions, worse under stress.

The portal main repo already registers `HybridCache` globally (`AddCaching` in `Infrastructure/Dependencies.cs`) with Redis as the L2 distributed backing in deployed environments. `ColoradoCbmsServiceBase` already accepts `HybridCache?` via constructor (currently used only by `MockCbmsDataStore`).

## Goals

1. **Cut user-visible latency.** Warm cache reduces a "view + change" flow from three CBMS calls to one.
2. **Encapsulate inside the CO plugin.** No state-connector contract changes; no other state plugins affected.
3. **Minimal surface area.** Single internal abstraction; three services collapse onto one dependency.
4. **Rock solid.** Stampede protection, write-through tripwire, clear failure semantics, full unit-test coverage.

## Non-goals

- Eliminating the slow first-load (cold cache). The product team is shipping an interstitial UI for this.
- Any caching for DC connector or state-connector contract changes.
- Cross-instance distributed coalescing of background refreshes (process-local is sufficient — see Stampede mitigation below).

## Design

### Architecture overview

```
┌─ CO Connector ──────────────────────────────────────────────────────────┐
│                                                                         │
│  ColoradoSummerEbtCaseService ─┐                                        │
│  ColoradoAddressUpdateService ─┼─► ICbmsHouseholdCache (NEW; internal)  │
│  ColoradoCardReplacementService┘     │                                  │
│                                      │ on miss / hard-expiry            │
│                                      ▼                                  │
│                              CbmsSebtApiClient (Kiota; existing)        │
│                                      │                                  │
│                                      ▼                                  │
│                              MockCbmsHttpHandler (existing) │ Real CBMS │
└─────────────────────────────────────────────────────────────────────────┘
```

The cache is the single point that calls `client.Sebt.GetAccountDetails.PostAsync`. All three services depend on the cache; none calls the Kiota client directly for `get-account-details`.

### Component 1: `ICbmsHouseholdCache` (new internal abstraction)

**Location:** `src/SEBT.Portal.StatePlugins.CO/Cbms/ICbmsHouseholdCache.cs`

```csharp
internal interface ICbmsHouseholdCache
{
    /// <summary>
    /// Returns the cached CBMS GetAccountDetailsResponse for the household,
    /// fetching from CBMS on miss or hard-expiry. On soft-expiry, returns the
    /// cached value and triggers a background refresh.
    /// Returns null if CBMS reports no household for the identifier.
    /// </summary>
    Task<GetAccountDetailsResponse?> GetAsync(
        string normalizedPhone,
        CancellationToken cancellationToken);

    /// <summary>
    /// Write-through: store the (locally-mutated) response after a successful PATCH.
    /// On underlying SetAsync failure, falls back to InvalidateAsync (tripwire).
    /// </summary>
    Task SetAsync(
        string normalizedPhone,
        GetAccountDetailsResponse value,
        CancellationToken cancellationToken);

    /// <summary>
    /// Explicit invalidation. Used by the tripwire and as an escape hatch for
    /// cases where the in-memory mutation cannot be safely produced.
    /// </summary>
    Task InvalidateAsync(
        string normalizedPhone,
        CancellationToken cancellationToken);
}
```

`internal` visibility is intentional: the cache is a CO-plugin implementation detail. The state-connector contract package is unchanged.

### Component 2: `CbmsHouseholdCacheEnvelope` (cached payload wrapper)

**Location:** `src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsHouseholdCacheEnvelope.cs`

```csharp
internal sealed record CbmsHouseholdCacheEnvelope(
    GetAccountDetailsResponse Response,
    DateTimeOffset SoftExpiryUtc,
    DateTimeOffset HardExpiryUtc,
    DateTimeOffset CachedAtUtc);
```

`SoftExpiryUtc` lives **inside** the cached payload, not as a `HybridCacheEntryOptions.Expiration` value. HybridCache's framework-level expiration is set to `HardExpiryUtc` so the framework evicts at the absolute ceiling. Soft-expiry is interpreted by `CbmsHouseholdCache` on every read.

### Component 3: `CbmsHouseholdCache` (the implementation)

**Location:** `src/SEBT.Portal.StatePlugins.CO/Cbms/CbmsHouseholdCache.cs`

#### Read flow

```
1. var key = $"co:cbms:{_identifierHasher.Hash(normalizedPhone)}";
2. var envelope = await _hybridCache.GetOrCreateAsync(
       key,
       factory: ct => FetchAndWrapAsync(normalizedPhone, ct),
       options: new HybridCacheEntryOptions { Expiration = _options.HardExpiration },
       cancellationToken: ct);
3. if (envelope is null) return null;                   // CBMS reported no household; negative cache
4. if (DateTimeOffset.UtcNow < envelope.SoftExpiryUtc)
       return envelope.Response;                        // fresh
5. TriggerBackgroundRefresh(key, normalizedPhone);      // stale-but-served (coalesced)
6. return envelope.Response;
```

`FetchAndWrapAsync`:
- Calls `client.Sebt.GetAccountDetails.PostAsync` with the requested phone.
- For a populated response, wraps in an envelope with `SoftExpiryUtc = now + soft`, `HardExpiryUtc = now + hard`.
- For a genuinely empty response (no enrollment rows / 404), returns a special "negative" envelope with a much shorter expiry (`NegativeCacheSeconds`, default 60 s) — prevents hammering CBMS while a household is being created upstream, but doesn't pin a "no household" answer for the full hard window.

#### Write-through flow

`SetAsync(phone, response, ct)`:
```
try {
    var envelope = new CbmsHouseholdCacheEnvelope(
        Response: response,
        SoftExpiryUtc: now + soft,
        HardExpiryUtc: now + hard,
        CachedAtUtc: now);
    await _hybridCache.SetAsync(key, envelope, hardEntryOptions, ct);
} catch (Exception ex) {
    _logger.LogWarning(ex, "Cache write-through failed for {KeyPrefix}; invalidating to force re-fetch", "co:cbms:*");
    try { await InvalidateAsync(phone, ct); } catch { /* best-effort secondary */ }
}
```

The tripwire (`InvalidateAsync` fallback) ensures we never serve a wrongly-mutated envelope: if write-through fails for any reason, the next read pays a CBMS round-trip and gets canonical state.

#### Background refresh (SWR coalescing)

```csharp
private readonly ConcurrentDictionary<string, Task> _inFlightRefreshes = new();

private void TriggerBackgroundRefresh(string key, string normalizedPhone)
{
    _inFlightRefreshes.GetOrAdd(key, k => RunRefreshAsync(k, normalizedPhone));
}

private async Task RunRefreshAsync(string key, string normalizedPhone)
{
    try
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(_appStopping);
        cts.CancelAfter(_options.BackgroundRefreshTimeout);
        var fresh = await FetchAndWrapAsync(normalizedPhone, cts.Token);
        if (fresh is not null)
        {
            await _hybridCache.SetAsync(key, fresh, hardEntryOptions, cts.Token);
        }
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Background CBMS refresh failed for cached household; stale envelope retained");
    }
    finally
    {
        _inFlightRefreshes.TryRemove(key, out _);   // load-bearing
    }
}
```

**Properties this guarantees:**
- Concurrent stale reads → exactly one background CBMS call per process per key (per soft-expiry boundary).
- Background fetch survives the originating HTTP request finishing (uses `_appStopping`-linked CTS, not the caller's token).
- Background fetch is bounded (default 60 s) — a hung CBMS call cannot pin a refresh task forever.
- Refresh failure leaves the stale envelope in place; the hard expiry will still force a synchronous fetch eventually.
- The `finally` block is load-bearing: forgetting to remove the in-flight entry would permanently block future refreshes for that key. Tested explicitly.

**Multi-instance reality check:** the in-flight dictionary is process-local. Across N portal pods, you can see up to N background fetches per key per soft-expiry. For the realistic N (small number of pods) this is acceptable; cross-instance coalescing would require a Redis-backed distributed lock and is judged not worth the complexity for the marginal load reduction.

#### Key shape and PII

```csharp
var key = $"co:cbms:{_identifierHasher.Hash(normalizedPhone)}";
```

- Namespace prefix `co:cbms:` is human-readable for diagnostic value (Redis `KEYS co:cbms:*` shows the keyspace at a glance).
- Variable part is HMAC-hashed via the existing `IIdentifierHasher` (HMAC-SHA256 with `IdentifierHasher:SecretKey`). Raw phone never enters the keyspace.
- The cached **value** is stored as-is. Defense-in-depth: the deployed Redis is encrypted at rest at the infrastructure layer (AWS ElastiCache AES-256). Application-layer payload encryption is not added; if that posture changes, an `IDataProtector`-wrapping serializer can be introduced later without changing the public surface of `ICbmsHouseholdCache`.

### Component 4: Service refactor

The three Colorado services collapse onto `ICbmsHouseholdCache`:

#### `ColoradoSummerEbtCaseService`
- Replace direct `client.Sebt.GetAccountDetails.PostAsync` call inside `GetHouseholdByPhoneAsync` with `_cache.GetAsync(normalizedPhone, ct)`.
- Mapping (`CbmsResponseMapper.MapToHouseholdData`) and `PiiVisibility` filtering remain in the service — the cache is intentionally PII-visibility-agnostic.

#### `ColoradoAddressUpdateService`
- Replace direct `get-account-details` call with `_cache.GetAsync(...)`.
- After a successful PATCH, **mutate the in-memory response** to reflect the updated address (copy the new address fields onto each matched `StdntEnrollDtl` row — same fields the PATCH body sent), then call `_cache.SetAsync(phone, mutatedResponse, ct)`.
- On PATCH failure, leave the cache unchanged.
- Inherit from `ColoradoCbmsServiceBase` and remove the duplicated `GetOrCreateClient` / `_clientCacheLock` / `_cachedClient` fields. (Cleanup carried into this change because the duplication would otherwise compound with the new cache injection.)

#### `ColoradoCardReplacementService`
- Replace direct `get-account-details` call with `_cache.GetAsync(...)`.
- **No write-through.** `reqNewCard="Y"` is a request input to the PATCH, not stored household state on subsequent reads. Card-replacement cooldown is tracked in the portal database (per main-repo CLAUDE.md guidance), not via the state connector. The cache simply provides the IDs needed for the PATCH.
- Inherit from `ColoradoCbmsServiceBase` for the same dedup reasons as address update.

### Component 5: Configuration

**Shape (`appsettings.co.json` and `appsettings.json` overlay):**

```jsonc
{
  "Cbms": {
    "Cache": {
      "SoftExpirationMinutes": 15,
      "HardExpirationMinutes": 240,
      "NegativeCacheSeconds": 60,
      "BackgroundRefreshTimeoutSeconds": 60
    }
  }
}
```

**Bound type (`CbmsHouseholdCacheOptions`):**

```csharp
internal sealed class CbmsHouseholdCacheOptions
{
    public int SoftExpirationMinutes { get; init; } = 15;
    public int HardExpirationMinutes { get; init; } = 240;
    public int NegativeCacheSeconds { get; init; } = 60;
    public int BackgroundRefreshTimeoutSeconds { get; init; } = 60;

    public TimeSpan SoftExpiration => TimeSpan.FromMinutes(SoftExpirationMinutes);
    public TimeSpan HardExpiration => TimeSpan.FromMinutes(HardExpirationMinutes);
    public TimeSpan NegativeCacheExpiration => TimeSpan.FromSeconds(NegativeCacheSeconds);
    public TimeSpan BackgroundRefreshTimeout => TimeSpan.FromSeconds(BackgroundRefreshTimeoutSeconds);

    public IEnumerable<string> Validate()
    {
        if (SoftExpirationMinutes <= 0) yield return "SoftExpirationMinutes must be > 0";
        if (HardExpirationMinutes <= 0) yield return "HardExpirationMinutes must be > 0";
        if (SoftExpirationMinutes >= HardExpirationMinutes) yield return "SoftExpirationMinutes must be < HardExpirationMinutes";
        if (NegativeCacheSeconds < 0) yield return "NegativeCacheSeconds must be >= 0";
        if (BackgroundRefreshTimeoutSeconds <= 0) yield return "BackgroundRefreshTimeoutSeconds must be > 0";
    }
}
```

Validated at startup via `IValidateOptions<CbmsHouseholdCacheOptions>`; misconfiguration fails fast.

### Component 6: Plugin DI registration

The CO plugin's host-bound DI registration adds the cache as a singleton, alongside the existing plugin services. Concretely, this is wired through the existing plugin DI bridge (main-repo ADR-0011) — the cache type is registered in the same path that today registers the Colorado* services, and plugin constructors receive it via `ImportingConstructor` parameters.

Dependencies of `CbmsHouseholdCache`:
- `HybridCache` — already registered globally
- `IConfiguration` — already passed to plugins
- `ILoggerFactory` — already passed to plugins
- `IIdentifierHasher` — already registered globally
- `IHostApplicationLifetime` — provides the `ApplicationStopping` token for background refresh cancellation
- `IOptions<CbmsHouseholdCacheOptions>` — bound from configuration
- `Func<CbmsConnectionOptions, CbmsSebtApiClient>` — a small factory provided by `ColoradoCbmsServiceBase` so the cache can fetch from CBMS on miss without duplicating client construction

The cache is registered as a singleton because the in-flight refresh dictionary must be shared across all callers.

### Mock-mode interaction

`MockCbmsHttpHandler` intercepts at the HTTP transport layer. The cache sits **above** the Kiota client, so it doesn't know or care whether responses are real or mocked. Mock-mode tests work identically to real-mode behavior — same cache flows, same write-through path, same SWR semantics.

The existing `MockCbmsDataStore` (which uses `HybridCache` for mock state) is unaffected; its keyspace (`cbms-mock:*`) is distinct from the new `co:cbms:*` keyspace.

## Test strategy

Full unit-test coverage on the cache. Tests are split into multiple files by logical grouping for human review ergonomics, all under `test/SEBT.Portal.StatePlugins.CO.Tests/Cbms/Cache/`:

### `CbmsHouseholdCacheReadTests.cs`
- Cache hit (fresh) — returns cached response, no CBMS call, no refresh triggered.
- Cache hit (stale: soft ≤ age < hard) — returns cached response **and** triggers exactly one background refresh.
- Cache miss — calls CBMS, populates with correct soft/hard expiries, returns response.
- Negative cache — null/empty CBMS response stored briefly per `NegativeCacheSeconds`; second call within window does not hit CBMS.
- Negative cache expires after `NegativeCacheSeconds` and re-fetches.
- Past hard expiry — synchronous fetch from CBMS.

### `CbmsHouseholdCacheWriteThroughTests.cs`
- `SetAsync` happy path — cache updated; subsequent `GetAsync` returns the new value; no CBMS call.
- `SetAsync` updates expiries to `now + soft` / `now + hard`.
- `SetAsync` tripwire — when the underlying `HybridCache.SetAsync` throws, falls back to `InvalidateAsync`; subsequent `GetAsync` calls CBMS.
- `InvalidateAsync` — entry removed; next read repopulates from CBMS.
- `InvalidateAsync` is idempotent (no-op when key absent).

### `CbmsHouseholdCacheBackgroundRefreshTests.cs`
- Stale read fires background refresh; refresh updates the cache; next read is fresh.
- Background refresh failure is logged; stale envelope retained; next stale read re-triggers.
- Background refresh respects `BackgroundRefreshTimeoutSeconds`.
- Background refresh respects `IHostApplicationLifetime.ApplicationStopping` (cancels cleanly on shutdown).
- Background refresh does **not** observe the originating caller's `CancellationToken` (caller token cancellation does not abort the refresh).

### `CbmsHouseholdCacheStampedeTests.cs`
- 50 concurrent stale reads → exactly one background CBMS call (assert via call counter on a stub CBMS client).
- 50 concurrent cold-cache reads → exactly one synchronous CBMS call (HybridCache framework coalescing).
- In-flight entry removed on success.
- In-flight entry removed on failure.
- In-flight entry removed on cancellation.
- New stale read after a completed refresh triggers a fresh in-flight task (entry was removed cleanly).

### `CbmsHouseholdCacheKeyHashingTests.cs`
- Cache key matches `co:cbms:{hash}` shape.
- Hash is produced by `IIdentifierHasher.Hash(normalizedPhone)`.
- Raw phone never appears in the cache key (regex assertion).
- Same input phone produces same key (idempotent).

### Service-level tests (in existing service test files, not duplicated)
- `ColoradoSummerEbtCaseServiceTests`: cache injection, `GetHouseholdByPhoneAsync` hits cache, no direct Kiota call.
- `ColoradoAddressUpdateServiceTests`: read-via-cache + PATCH success → `SetAsync` called with response whose address fields match the request; PATCH failure → no cache mutation.
- `ColoradoCardReplacementServiceTests`: read-via-cache + PATCH; **no** `SetAsync` call asserted explicitly.
- Mock-mode parity: with `Cbms:UseMockResponses=true`, full read/write flow exercises the cache identically.

### Coverage targets
- 100% line + branch on `CbmsHouseholdCache` and `CbmsHouseholdCacheEnvelope`.
- ≥90% on touched paths in the three Colorado services.

## Failure-mode summary

| Scenario | Today | With cache |
|---|---|---|
| CBMS up, cache cold | 8-9 s read + ~1 s PATCH | Same |
| CBMS up, cache warm | n/a | < 50 ms read + ~1 s PATCH |
| CBMS down, cache cold | Failure (existing error mapping) | Same |
| CBMS down, cache warm (fresh) | n/a | Read succeeds from cache; PATCH fails with existing mapping |
| CBMS down, cache stale (background refresh fires) | n/a | Read succeeds (stale); refresh fails silently; logged |
| Redis down | n/a | HybridCache L2 disabled, L1 in-process still works; no functional change |
| Both Redis and L1 cold | Equivalent to "no cache" — direct CBMS call | Same — cache is purely additive |
| Cache write-through fails after PATCH | n/a | Tripwire invalidates; next read re-fetches canonical state |

No path produces a "wrongly-mutated cache served as truth" outcome.

## Future cleanup (out of scope)

- **DC adoption.** If DC ever needs the same pattern, `ICbmsHouseholdCache` could be promoted to a `IStateHouseholdCache<TResponse>` generic in the state-connector contracts package. Not required today — DC's read latency is not in the same neighborhood.
- **Cross-instance coalescing.** If pod count grows large enough that N background fetches per soft-boundary is meaningful CBMS load, a Redis-backed distributed lock could be added.
- **Application-layer payload encryption.** If infrastructure-layer encryption-at-rest is ever insufficient, swap the HybridCache serializer for one that wraps with `IDataProtector`.
- **Observability.** Add counters / structured logs for cache hit, stale-served, miss, refresh-success, refresh-failure, write-through, tripwire-invalidate. Useful for dashboards but not required for correctness.
