# PR #31 Review Priority Guide — CO CBMS Household Cache

**Risk Level:** 🟡 Medium &nbsp;|&nbsp; **Files:** 32 (+5,203 / -237) &nbsp;|&nbsp; **Branch:** `feature/co-cbms-household-cache`

This PR introduces a cache-aside + stale-while-revalidate + write-through layer over Colorado's slow CBMS `get-account-details` endpoint and refactors all three Colorado services to use it. The most consequential code is concentrated in two new files (`CbmsHouseholdCache.cs`, `PluginCache.cs`) and the address-update service (the only one with write-through). A typical "view → submit address change" flow drops from ~17–19s to ~1s.

**Companion PRs that must merge first** (NuGet/binary chain): state-connector [#20](https://github.com/codeforamerica/sebt-self-service-portal-state-connector/pull/20) → main-portal [#244](https://github.com/codeforamerica/sebt-self-service-portal/pull/244) → this PR.

---

## Priority Review Table

| Priority | File | Function/Change | Lines | Sec | Cpx | Nov | Notes |
|:--:|----|----|:--:|:--:|:--:|:--:|----|
| 🔴 | [`src/.../Cbms/Cache/CbmsHouseholdCache.cs`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-cbmshouseholdcache) | entire file (NEW) | 1-167 | 🟡 | 🔴 | 🟡 | Concurrent SWR, in-flight registry, sentinel pattern |
| 🔴 | [`src/.../Cbms/Cache/PluginCache.cs`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-plugincache) | `GetOrBuild` (NEW) | 24-104 | 🟡 | 🟡 | 🔴 | Static lazy + `ActivatorUtilities` + mock-mode |
| 🟡 | [`src/.../ColoradoAddressUpdateService.cs`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-coloradoaddressupdate) | `UpdateAddressAsync` + write-through | full | 🟡 | 🟡 | 🟡 | Mutates cache after PATCH; verify field parity |
| 🟡 | [`src/.../ColoradoCbmsServiceBase.cs`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-coloradobase) | dual-ctor + `HouseholdCache` | 11-50 | 🟢 | 🟡 | 🟡 | Transitional 2-ctor state; cleanup deferred |
| 🟡 | [`src/.../ColoradoCardReplacementService.cs`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-cardreplacement) | `RequestCardReplacementAsync` | full | 🟢 | 🟡 | 🟡 | Mirrors AddressUpdate; confirm no write-through |
| 🟡 | [`docs/adr/0004-cbms-response-caching.md`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-adr) | architectural decision | full | 🟢 | 🟢 | 🔴 | Read first; sets context for everything else |
| 🟡 | [`src/.../Cbms/Cache/PluginCache.cs`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-plugincache) | `OverrideForTesting` / `ResetForTesting` | 92-104 | 🟢 | 🟢 | 🟡 | Static-state test seam; verify lock discipline |
| 🟢 | [`src/.../ColoradoSummerEbtCaseService.cs`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-summerebt) | `GetHouseholdByPhoneAsync` refactor | full | 🟢 | 🟢 | 🟢 | Read leg now via cache; same exception handling |
| 🟢 | [`src/.../Cbms/Cache/CbmsHouseholdCacheOptions.cs`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-cacheopts) | options + `Validate()` | 1-23 | 🟢 | 🟢 | 🟢 | Plain config bag |
| 🟢 | [`src/.../Cbms/Cache/CbmsHouseholdCacheEnvelope.cs`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-envelope) | record (NEW) | 1-9 | 🟢 | 🟢 | 🟢 | One-line record |
| 🟢 | [`src/.../Cbms/Cache/ICbmsHouseholdCache.cs`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-icache) | interface (NEW) | 1-25 | 🟢 | 🟢 | 🟢 | 3-method internal interface |
| 🟢 | [`src/.../Cbms/Cache/CbmsFetchAccountDetailsDelegate.cs`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-fetchdelegate) | delegate type (NEW) | 1-12 | 🟢 | 🟢 | 🟢 | Test seam for CBMS call |
| 🟢 | [`src/.../Cbms/CbmsAddressUpdateMapper.cs`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-mapper) | `ApplyAddressToRow` (NEW) | added | 🟢 | 🟢 | 🟢 | Mirror of `ToCbmsAddress` field set |
| 🟢 | [`src/.../SEBT.Portal.StatePlugins.CO.csproj`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-csproj) | wildcard fix + InternalsVisibleTo | small | 🟢 | 🟢 | 🟢 | NuGet wildcard `0.0.2-*`; NSubstitute proxy |
| 🟢 | `src/.../Cache/Cbms*Tests.cs` (7 files) | new cache test files | each ≤123 | 🟢 | 🟢 | 🟢 | Skim for coverage; FakeFetch + InMemoryHybridCache helpers |
| 🟢 | `src/.../Tests/Colorado*ServiceTests.cs` (3 files) | reworked service tests | mod | 🟢 | 🟢 | 🟢 | Verify cache-routing assertions exist |
| 🟢 | [`src/.../PluginCacheCollection.cs`](https://github.com/codeforamerica/sebt-self-service-portal-co-connector/pull/31/files#diff-collection) | xUnit collection (NEW) | 1-10 | 🟢 | 🟢 | 🟢 | Serializes static-state tests |
| 🟢 | `docs/superpowers/specs/...design.md` | design spec | 362 | 🟢 | 🟢 | 🟢 | Reference; matches ADR |
| 🟢 | `docs/superpowers/plans/...plan.md` | implementation plan | 2766 | 🟢 | 🟢 | 🟢 | History; optional reading |

---

## Summary by Priority

### 🔴 Critical Review (2 items)

1. **`CbmsHouseholdCache.cs` (NEW, 167 lines)** — The cache implementation itself. Most reviewer attention belongs here:
   - **Read flow** (lines 51-71): cache-miss factory, soft-expiry → return-stale-and-trigger-refresh, sentinel envelope detection. Is the soft/hard/negative-expiry interaction correct?
   - **`FetchAndWrapAsync`** (lines 73-95): the negative-cache sentinel is a `static readonly` instance compared with `ReferenceEquals` — value equality on the record would yield false matches. Worth a careful read.
   - **`TriggerBackgroundRefresh` + `RunRefreshAsync`** (lines 97-134): the in-flight `ConcurrentDictionary<string, Task>` is the stampede protection. The `finally` block's `TryRemove` is **load-bearing** — if it ever doesn't run, that key is permanently blocked from refreshing. The `IHostApplicationLifetime.ApplicationStopping`-linked CTS + bounded timeout protects against hung CBMS calls.
   - **Write-through tripwire** (`SetAsync` → catch → `InvalidateAsync`): on cache-write failure, fall back to invalidate so the next read repopulates from CBMS canonically.
   - **What's NOT covered:** the design spec called out one test (cross-round in-flight registry cleanup) that proved racy under the in-memory cache helper and was removed with a documented coverage gap. The first stampede test (50 concurrent → 1 fetch) covers the core property; the gap is documented in `CbmsHouseholdCacheStampedeTests.cs` near the bottom of the file.

2. **`PluginCache.cs` (NEW, 106 lines)** — Static lazy singleton for the cache instance:
   - **`GetOrBuild`** (lines 24-90): double-checked lock + `ActivatorUtilities.CreateInstance` against the host service provider. Worth verifying the locking discipline and that all required services are resolvable from the host (`HybridCache`, `IHMACSHA256Hasher`, `IHostApplicationLifetime`, `ILoggerFactory`).
   - **Mock-mode plumbing** (lines 47-58): when `Cbms:UseMockResponses=true`, the cache wires up `MockCbmsHttpHandler` directly. This duplicates the mock-handler creation that exists in `ColoradoCbmsServiceBase.GetOrCreateClient` — intentional (the cache builds its OWN client at startup; the base's per-service clients are for the PATCH path). Worth confirming the duplication is acceptable.
   - **`OverrideForTesting` + `ResetForTesting`**: gated by `[InternalsVisibleTo("SEBT.Portal.StatePlugins.CO.Tests")]`. Tests use `[Collection("PluginCache")]` to serialize, since static state would otherwise leak across parallel test runs.

### 🟡 Recommended Review (5 items)

3. **`ColoradoAddressUpdateService.cs`** — The only service that writes through. After successful PATCH:
   ```csharp
   foreach (var (row, _) in actionable) {
       CbmsAddressUpdateMapper.ApplyAddressToRow(request.Address, row);
   }
   await HouseholdCache!.SetAsync(phone10, accountResponse, cancellationToken);
   ```
   Verify the field set in `ApplyAddressToRow` matches what `ToCbmsAddress` actually sends in the PATCH body — otherwise the cache and CBMS will diverge until the next hard-expiry refresh. The implementer used `SplitPostalCode` to derive `Zip`/`Zip4` to mirror PATCH semantics; confirm.

4. **`ColoradoCbmsServiceBase.cs`** — Now has two constructors: full (with `IServiceProvider`+`IConfiguration`, sets `HouseholdCache`) and minimal (no cache). `ColoradoEnrollmentCheckService` retains the minimal constructor since it doesn't yet need cache access. The `HouseholdCache` property is `private protected ICbmsHouseholdCache?` (nullable) — non-null when constructed via the full ctor; consumers use `HouseholdCache!.GetAsync(...)`.

5. **`ColoradoCardReplacementService.cs`** — Same shape as AddressUpdate but **no write-through**. `reqNewCard="Y"` is a request input, not stored state, and card-replacement cooldown is tracked in the portal database. Verify that no `HouseholdCache.SetAsync` or `InvalidateAsync` is invoked in this service.

6. **`PluginCache.OverrideForTesting` / `ResetForTesting`** — Used by the test class fixtures and the `PluginCacheCollection`. Lock discipline matters: tests calling `OverrideForTesting` from non-locked contexts could see torn state.

7. **`docs/adr/0004-cbms-response-caching.md`** — Read before the code. Captures the decision rationale, alternatives rejected (decorator, contract-level abstraction, separate ID cache, mutate + post-write refresh, invalidate-on-write), and the multi-repo footprint. The spec amend at the bottom acknowledges the cross-repo `IHMACSHA256Hasher` addition.

### 🟢 Low Priority (rest)

- **Foundation types** (`Options`, `Envelope`, `ICache`, `FetchDelegate`): trivial — read for completeness.
- **`CbmsAddressUpdateMapper.ApplyAddressToRow`**: 21 lines of mechanical field copies; cross-check with `ToCbmsAddress`.
- **csproj changes**: NuGet wildcard fix (`0.0.2-dev-*` → `0.0.2-*` to match published `0.0.2-dev` package shape) + `InternalsVisibleTo("DynamicProxyGenAssembly2")` for NSubstitute proxies of internal interfaces.
- **Test files**: 7 new cache test files (~700 lines total). Skim for coverage breadth; the cache class itself has 26 dedicated tests across 5 logical groupings (read, write-through, background refresh, stampede, key-hashing) plus 3 PluginCache tests.
- **Design spec + plan + sandbox tests**: reference material.

---

## Review Recommendation

**Suggested reading order:**

1. **ADR (0004)** — sets context for every code decision below.
2. **`CbmsHouseholdCache.cs`** — the core. Pay particular attention to the in-flight registry lifecycle and the sentinel pattern.
3. **`PluginCache.cs`** — verify locking, mock-mode wiring, test seam.
4. **`ColoradoAddressUpdateService.cs`** + **`CbmsAddressUpdateMapper.ApplyAddressToRow`** — confirm field parity between cache mutation and PATCH body.
5. **`ColoradoCbmsServiceBase.cs`** — confirm dual-ctor transitional state is acceptable; flag follow-up for `EnrollmentCheckService` migration.
6. **`ColoradoCardReplacementService.cs`** — skim; confirm no write-through.
7. **`ColoradoSummerEbtCaseService.cs`** — skim.
8. **Tests** — skim each cache test file to confirm coverage of the documented properties; verify the test-collection serialization is in place.

**No 🔴 security concerns identified.** PII at rest is handled per ADR-0004 (HMAC'd cache keys, infrastructure-layer encryption-at-rest on Redis). PII in transit between cache and Redis is the same channel the existing system uses for `IIdentifierHasher` storage — no new exposure.

**Open follow-ups noted in the PR description** (out of scope for this review):
- Casing-compat audit before B4 (refactoring `IIdentifierHasher` to delegate to `IHMACSHA256Hasher`).
- `ColoradoEnrollmentCheckService` migration to the full base constructor when it eventually needs cache access.
- A deterministic version of the in-flight registry cleanup test (currently a documented coverage gap).
