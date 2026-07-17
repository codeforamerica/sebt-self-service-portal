# 4. Cache CBMS get-account-details responses with stale-while-revalidate and write-through

Date: 2026-04-30

## Status

Accepted

## Context

The Colorado CBMS `get-account-details` endpoint averages 8-9 seconds per call and has been observed at 30+ seconds. The companion PATCH endpoint (`update-std-dtls`) is fast — averaging under 1 second. The latency problem is the read leg, not the write leg. Three CO connector services hit `get-account-details`:

- `ColoradoSummerEbtCaseService` reads household data for portal display.
- `ColoradoAddressUpdateService` reads (to obtain CBMS internal IDs) before PATCHing `update-std-dtls`.
- `ColoradoCardReplacementService` reads (to obtain CBMS internal IDs) before PATCHing `update-std-dtls`.

Because the write services need raw CBMS IDs (`sebtChldId`, `sebtAppId`, `sebtChldCwin`) that the canonical portal `HouseholdData` does not carry, today's address-change and card-replacement flows make two slow `get-account-details` calls each (display read + write-handler re-read), then a fast PATCH. A "view → submit address change" flow runs roughly 17-19 seconds under average conditions (8-9s + 8-9s + ~1s), worse under stress.

The portal main repo already registers `HybridCache` globally with Redis as the L2 distributed backing in deployed environments. Application-level encryption-at-rest is provided by AWS ElastiCache (AES-256). The CO plugin's existing `ColoradoCbmsServiceBase` already accepts `HybridCache?` via constructor.

## Decision

Introduce an internal `ICbmsHouseholdCache` abstraction inside the CO plugin. All three Colorado services depend on it; none calls the CBMS `get-account-details` endpoint directly. The cache is the single point of contact with that endpoint.

Behavior:

- **Cache-aside.** On miss or past-hard-expiry, the cache calls CBMS synchronously, populates, and returns. On hit, returns the cached `GetAccountDetailsResponse` directly.
- **Stale-while-revalidate (SWR).** A two-tier expiry — soft (default 15 min, configurable) and hard (default 4 hr, configurable) — lives inside the cached envelope. Reads past soft-expiry return the cached value and trigger a background refresh; reads past hard-expiry block on a synchronous CBMS call. HybridCache's framework expiration is set to the hard value so the framework evicts at the absolute ceiling.
- **Stampede coalescing.** A process-local `ConcurrentDictionary<string, Task>` ensures concurrent stale reads spawn at most one background CBMS call per key per soft-expiry boundary. Background refreshes run on a `IHostApplicationLifetime.ApplicationStopping`-linked `CancellationTokenSource` with a hard timeout (default 60 s, configurable) so they survive the originating HTTP request finishing without pinning a hung task.
- **Write-through with tripwire.** `ColoradoAddressUpdateService` mutates the cached response in memory after a successful PATCH and writes it back via `SetAsync`. If the cache write fails for any reason, the cache invalidates the entry so the next read pays a CBMS round-trip and gets canonical state. `ColoradoCardReplacementService` does **not** write through, because `reqNewCard="Y"` is a request input not a stored field, and card-replacement cooldown is tracked in the portal database.
- **PII at rest.** Cache keys are shaped `co:cbms:{hash}` where the hash is produced by `IHMACSHA256Hasher` (see below). Raw phone numbers never enter the keyspace. Cached values rely on infrastructure-layer encryption-at-rest (ElastiCache AES-256).
- **Negative caching.** Empty/no-household responses are cached briefly (default 60 s) to avoid hammering CBMS during upstream household creation, but not for the full hard window.
- **`IHMACSHA256Hasher` primitive in the state-connector contracts package.** A new domain-agnostic HMAC-SHA256 hasher contract is added to the contracts package with a portal-side implementation reusing `IdentifierHasher:SecretKey`. The CO plugin consumes it via DI for cache-key hashing. Future plugins can reuse the same primitive. A follow-up may refactor the existing `IIdentifierHasher` to delegate to this new primitive (deferred — requires a casing-compatibility audit because `IdentifierHasher.Hash` currently returns uppercase hex while the new primitive returns lowercase; persisted-hash equality uses byte-level comparison via `FromHexString` which is case-insensitive, so the change appears safe but warrants verification).
- **Plugin-private singleton via static `PluginCache`.** The CO plugin holds the `ICbmsHouseholdCache` instance in a static field, lazily constructed via `ActivatorUtilities.CreateInstance` against the host service provider on first plugin construction. Tests substitute a fake via `PluginCache.OverrideForTesting`. This avoids extending the main-portal plugin DI bridge while preserving full singleton semantics (so the in-flight refresh registry is shared across all three Colorado services).

The cache and its plumbing are internal to the CO plugin. The state-connector contract package gains exactly one new primitive (`IHMACSHA256Hasher`); DC connector ignores it (no DC code changes required). The main portal adds one implementation class and one DI registration line.

### Alternatives considered

- **Decorator on `ISummerEbtCaseService`.** Cleanly wraps the read path but does nothing for the address-update and card-replacement paths, which would continue making their own slow `get-account-details` calls. Rejected — it leaves the larger half of the latency problem on the floor.
- **Public abstraction in the state-connector contracts package.** Most flexible (DC could adopt later) but introduces a contract-package version bump, requires the multi-repo build dance for every plugin, and increases blast radius for what is today a Colorado-specific problem. Rejected — premature.
- **A separate "phone → IDs" cache alongside the response cache.** Considered as a way to skip the read leg of cold-cache write flows. Rejected — the IDs already live on each row of the `GetAccountDetailsResponse` we are caching, so caching them separately is redundant; the only scenario where it would help is when the response cache is cold but the IDs cache is warm, which implies the user reached a write flow without first viewing their data (rare). The complexity cost of two-key consistency outweighs the marginal cold-write speedup.
- **Mutate-in-place + post-write background refresh** (as opposed to mutate-in-place alone). Rejected — it depends on CBMS read-after-write consistency that we cannot verify without a documented contract; in the worst case, a background refresh could fetch pre-PATCH state and overwrite a correctly-mutated cache with stale data. The 15-minute soft-expiry SWR already provides drift correction at no extra risk.
- **Invalidate-on-write (instead of mutate-in-place).** Rejected — penalizes the user immediately after the action they just took (the redirect-to-confirmation page would pay a full CBMS round-trip).

## Consequences

- **Latency.** Warm-cache reads return in milliseconds. A typical "view → submit address change" flow drops from ~17-19 s (two slow `get-account-details` calls + fast PATCH) to roughly 1 s (two cache hits + fast PATCH). Card-replacement drops similarly.
- **Cold-start cost is unchanged.** First read of a household after cache eviction still pays the full CBMS cost. The product team is shipping an interstitial UI to absorb this.
- **CBMS-side staleness window.** Up to 15 minutes for portal users to observe changes a caseworker made directly in CBMS (soft-expiry triggers a background refresh; the user is still served stale data on that read, but subsequent reads are fresh). Worst case 4 hours before a synchronous fetch is forced. Acceptable for a portal where caseworker mutations are rare.
- **Multi-instance coalescing is best-effort.** Stampede protection is process-local. Across N portal pods, up to N background fetches per key per soft-boundary are possible. For realistic N this is acceptable; cross-instance coalescing via Redis distributed lock is left as a future option.
- **Cross-pod L1 staleness on write-through.** `HybridCache.SetAsync` updates the writing pod's L1 + the shared L2 (Redis), but does NOT propagate to other pods' L1 caches. A user's next read landing on a different pod can serve pre-write data until that pod's L1 entry expires. The deployed environment does not use sticky sessions, so any request can land on any pod — we therefore set `LocalCacheExpiration` to 1 second by default (effectively bypassing L1) so reads always fall through to L2 (Redis) for fresh data. The knob is configurable via `Cbms:Cache:LocalCacheExpirationSeconds`. If sticky sessions are introduced or Redis read load becomes a concern, raise the value to trade staleness for L1 cache efficiency. A SWR refresh (≤ `SoftExpiration`) is a secondary safety net. The next-step mitigation if even that proves insufficient is Redis pub/sub keyspace notifications driving cross-pod L1 invalidation — deferred until measurably needed.
- **Service refactor.** `ColoradoAddressUpdateService` and `ColoradoCardReplacementService` will inherit from `ColoradoCbmsServiceBase` (today only `ColoradoSummerEbtCaseService` does), removing duplicated `GetOrCreateClient` / `_clientCacheLock` / `_cachedClient` fields. This dedup happens in the same change because the duplication would otherwise compound with the new cache injection.
- **Minimal cross-repo footprint.** State-connector adds one primitive interface (`IHMACSHA256Hasher`); main portal adds one implementation + one DI registration; the cache itself + the three Colorado service refactors live in this repo. DC connector requires no source changes (it picks up the new contracts NuGet but doesn't reference the new type).
- **`ColoradoEnrollmentCheckService` retains the minimal base-class constructor** (no `IServiceProvider`/`IConfiguration`) because it doesn't yet use the cache. The base class supports two constructors during this transition. When `EnrollmentCheckService` eventually needs cache access, it migrates to the full constructor and the minimal one can be removed — flagged as future cleanup.
- **Failure modes are additive, not subtractive.** Every cache failure path (Redis down, cache write throws, refresh times out, etc.) degrades to the existing direct-CBMS behavior. The cache is purely additive.
- **Configuration is environment-overridable.** TTLs, negative-cache window, and background-refresh timeout are bound from `Cbms:Cache:*` and validated at startup.
- **Test surface grows.** Full unit-test coverage (target: 100% line + branch on the cache; ≥90% on touched service paths) is committed to as part of the change. Tests are split into multiple files by logical grouping (read, write-through, background refresh, stampede, key-hashing) for human review ergonomics.
