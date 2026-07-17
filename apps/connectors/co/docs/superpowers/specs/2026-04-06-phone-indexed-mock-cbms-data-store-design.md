# Design: Phone-Indexed Mock CBMS Data Store

**Date:** 2026-04-06
**Repos:** sebt-self-service-portal-co-connector (primary), sebt-self-service-portal (plugin DI bridge)
**Branch:** feature/realistic-cbms-mock-responses
**Status:** Draft

## Purpose

Replace the single-response mock CBMS handler with a phone-indexed, cache-backed data store that returns realistic per-household responses. Enable PATCH mutations so the mock behaves like a stateful API across requests and across deployed instances.

## Background

The current `MockCbmsHttpHandler` returns the same hardcoded JSON for every `get-account-details` request regardless of input phone number. This limits testing to a single household shape and doesn't support the PATCH `update-std-dtls` endpoint at all. As the portal matures, testers and developers need multiple household scenarios (different family sizes, eligibility mixes, card statuses) accessible simultaneously.

## Design

### Architecture overview

```
┌─ Main Repo (sebt-self-service-portal) ─────────────────────────────┐
│  ServiceCollectionPluginExtensions                                 │
│    → Register HybridCache (+ Redis in deployed envs)               │
│    → Replace MEF composition with assembly type scanning           │
│    → Register non-health-check plugins as DI factory singletons    │
│    → Keep IStateHealthCheckService plugins eager (current behavior)│
└────────────────────────────────────────────────────────────────────┘
        │ HybridCache injected via DI constructor
        ▼
┌─ CO Connector ───────────────────────────────────────────────────┐
│                                                                  │
│  ColoradoSummerEbtCaseService                                    │
│    → Accepts HybridCache via constructor (nullable, optional)    │
│    → In mock mode: creates MockCbmsDataStore(hybridCache)        │
│    → Passes data store to MockCbmsHttpHandler                    │
│                                                                  │
│  MockCbmsDataStore (NEW — CbmsApi/Mocks/)                        │
│    → Loads mock-manifest.json + per-household embedded JSON      │
│    → Seeds into HybridCache on first access                      │
│    → GetResponseForPhone(phone) → JSON string or empty success   │
│    → ApplyPatch(request) → mutates cached household data         │
│    → Thread-safe via HybridCache                                 │
│                                                                  │
│  MockCbmsHttpHandler (MODIFIED)                                  │
│    → get-account-details: reads phone from request body,         │
│      delegates to MockCbmsDataStore                              │
│    → update-std-dtls: reads PATCH body, delegates to data store  │
│    → token, ping, check-enrollment: unchanged                    │
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### Component 1: Plugin DI bridge (main repo)

**Goal:** Allow plugin constructors to receive any DI-registered service, not just the two services (`IConfiguration`, `ILoggerFactory`) currently exported into the MEF container.

**Approach:** Replace MEF's composition/instantiation step with direct assembly type scanning and DI factory registration. MEF's assembly loading infrastructure (`WithAssembliesInPath`) is retained — it handles the hard work of loading plugin assemblies, resolving transitive dependencies, and avoiding duplicate loads. Only the composition step (`CreateContainer().GetExports<T>()`) is replaced.

**Changes to `ServiceCollectionPluginExtensions`:**

1. Call `services.AddHybridCache()` before plugin loading (with Redis `IDistributedCache` backing in deployed environments; in-memory fallback for local dev).

2. `WithAssembliesInPath` is refactored to return loaded `Assembly` objects (instead of a `ContainerConfiguration`).

3. Scan returned assemblies for concrete types implementing `IStatePlugin`:

    ```
    loadedAssemblies
        .SelectMany(a => a.GetExportedTypes())
        .Where(t => typeof(IStatePlugin).IsAssignableFrom(t) && !t.IsAbstract)
    ```

4. For each discovered plugin type, determine its single service interface (same validation as today: must implement exactly one interface beyond `IStatePlugin`).

5. Register based on interface type:
    - **`IStateHealthCheckService`**: Instantiate eagerly via `ActivatorUtilities.CreateInstance` using a temporary service provider built from the current `IServiceCollection`. At this point in startup, the collection contains ASP.NET core services, `IConfiguration`, `ILoggerFactory`, `HybridCache`, etc. — so the temporary provider can resolve these. Call `ConfigureHealthChecks(healthChecksBuilder)` on the instance, then register it as a singleton instance. Leave a code comment on the eager path explaining: (1) this uses a temporary service provider with its own singleton scope, (2) it works today because health check plugins only depend on `IConfiguration` and `ILoggerFactory` which are already fully constructed, (3) if a health check plugin ever needs a DI service with shared state (e.g., `HybridCache`), this approach should be revisited — e.g., by deferring health check registration or switching to type-based registration with a lazy resolve adapter.
    - **All other plugin interfaces**: Register as a singleton factory that calls `ActivatorUtilities.CreateInstance(sp, pluginType)` on first resolve (using the _real_ service provider), with info-level logging of the plugin type name on construction.

6. The `CreateContainerConfiguration` method and its MEF convention builders (`ConventionBuilder`, `.ForTypesDerivedFrom<T>()`, etc.) are removed.

**Code comments:** Leave a `// TODO: Remove System.Composition dependency` comment on the `using` statement and a block comment at the top of the method explaining:

- Why MEF is still referenced (assembly loading reuse, `[Export]` attributes still present on plugins)
- That plugins are instantiated by DI, not MEF
- The natural next step is extracting the assembly loading into a standalone helper and removing `System.Composition` entirely

**Impact on existing plugins:** Transparent. Plugin constructors already declare their dependencies via parameters — they'll work the same way whether MEF or DI creates them. The `[ImportingConstructor]`, `[Import]`, `[Export]`, and `[ExportMetadata]` attributes become inert but harmless.

### Component 2: Mock manifest and household JSON files (CO connector)

**File:** `TestData/CbmsMocks/mock-manifest.json`

A simple JSON mapping from normalized 10-digit phone numbers to household response filenames:

```json
{
    "households": {
        "3035550199": "household-smith.json",
        "7198004382": "household-alden.json"
    }
}
```

Phone numbers in the manifest must be exactly 10 digits with no punctuation (matching `NormalizePhone` output, e.g., `3035550199`). Note: the existing `get-account-details.json` uses phone `555-0123` which is only 7 digits and would fail lookup. This file should be updated with a valid 10-digit phone or replaced by the new household files.

**Household JSON files** live alongside the manifest in `TestData/CbmsMocks/`, marked as `EmbeddedResource` in the `.csproj`. Each file is a complete `GetAccountDetailsResponse` body (same shape as the existing `get-account-details.json`).

**Manifest generation:** The manifest is generated once from the household files by extracting the `gurdPhnNm` field from the first student in each file. This is a one-time step when new test data is added; the manifest is then checked in alongside the data files.

### Component 3: MockCbmsDataStore (CO connector — new class)

**Location:** `SEBT.Portal.StatePlugins.CO.CbmsApi/Mocks/MockCbmsDataStore.cs`

**Constructor:** `MockCbmsDataStore(HybridCache cache)`

**Responsibilities:**

1. **Seed on first access:** Load `mock-manifest.json` from embedded resources. For each entry, load the corresponding household JSON file and store it in `HybridCache` under a cache key like `cbms-mock:{phone}`. Use `HybridCache.GetOrCreateAsync` so that in a multi-instance deployment, only one instance seeds each entry (stampede protection is built in).

2. **`GetResponseForPhone(string normalizedPhone) → string`:** Look up `cbms-mock:{phone}` in the cache. If found, return the JSON string. If not found, return an empty success response:

    ```json
    { "stdntEnrollDtls": [], "respCd": "00", "respMsg": "Success" }
    ```

3. **`ApplyPatchAsync(string requestBodyJson) → string`:** Parse the `UpdateStudentDetailsRequest` fields from the request body using `System.Text.Json`. Use `sebtChldId` to find the matching student across all cached households. Apply mutations to the cached JSON:
    - Address fields (`addrLn1`, `addrLn2`, `cty`, `staCd`, `zip`, `zip4`) — update on the matched student
    - Guardian fields (`gurdFstNm`, `gurdLstNm`, `gurdEmailAddr`) — update on the matched student
    - `reqNewCard` — if `"Y"`, could simulate card reissue by clearing `ebtCardLastFour`/`ebtCardSts` (or leave as a no-op that returns success)

    Write the mutated JSON back to the cache. Return a success response:

    ```json
    { "respCd": "00", "respMsg": "Success" }
    ```

    If no student matches the `sebtChldId`, return a 404-shaped error response.

**Thread safety:** Achieved via `HybridCache`. Read operations use `GetOrCreateAsync`. Write operations (PATCH) use a read-modify-write pattern. Since `HybridCache` doesn't provide atomic compare-and-swap, PATCH operations use `SetAsync` to overwrite. In practice, concurrent PATCHes to the same household are unlikely in test scenarios, and last-write-wins is acceptable behavior. If this ever becomes a concern, we can add a `SemaphoreSlim` per phone number.

**Cache key format:** `cbms-mock:{normalizedPhone}` (e.g., `cbms-mock:7198004382`)

**Cache expiration:** No expiration. Mock data lives for the lifetime of the cache. In local dev (in-memory), data resets on app restart. In deployed environments (Redis), data persists until the Redis instance is recycled or explicitly flushed.

### Component 4: MockCbmsHttpHandler modifications (CO connector)

**Changes to existing `MockCbmsHttpHandler`:**

1. **Constructor** now accepts `MockCbmsDataStore`:

    ```
    MockCbmsHttpHandler(MockCbmsDataStore dataStore)
    ```

    The `return404ForGetAccountDetails` flag is removed — that behavior is now handled naturally by looking up a phone that has no mock data (returns empty success).

2. **`get-account-details` route:** Read the request body, extract `phnNm`, call `dataStore.GetResponseForPhone(phone)`, return the result as a JSON HTTP response.

3. **`update-std-dtls` route (NEW):** Read the request body, call `dataStore.ApplyPatchAsync(body)`, return the result as a JSON HTTP response. Match on `PATCH` method and URL containing `sebt/update-std-dtls`.

4. **Token, ping, check-enrollment:** Unchanged (static embedded JSON responses).

5. **SendAsync becomes async:** Currently returns `Task.FromResult` synchronously. With cache-backed operations, `get-account-details` and `update-std-dtls` routes will `await` the data store. Other routes remain synchronous via `Task.FromResult`.

### Component 5: ColoradoSummerEbtCaseService changes (CO connector)

1. **Constructor** adds an optional `HybridCache?` parameter:

    ```csharp
    [ImportingConstructor]
    public ColoradoSummerEbtCaseService(
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        HybridCache? cache = null)
    ```

2. **`GetOrCreateClient`** — when `UseMockResponses` is true, create a `MockCbmsDataStore` with the injected cache and pass it to `MockCbmsHttpHandler`:
    ```csharp
    var dataStore = new MockCbmsDataStore(cache!);
    var handler = new MockCbmsHttpHandler(dataStore);
    ```
    If `cache` is null and mock mode is enabled, throw an `InvalidOperationException` with a clear message explaining that `HybridCache` must be registered for mock mode. This is a configuration error that should be caught immediately at startup, not silently degraded.

### Existing mock JSON file disposition

- `get-account-details.json` — Becomes one of the household files (or is replaced by the new realistic files). Its phone number (`555-0123`) gets an entry in the manifest.
- `get-account-details.actual.json` — This was a sandbox data capture for reference. It can be converted into a manifest entry or left as documentation. It won't be loaded by the data store unless added to the manifest.
- `token.json`, `ping.json`, `check-enrollment.json` — Unchanged, still loaded as static embedded resources by `MockCbmsHttpHandler`.

### Test strategy

**MockCbmsDataStore unit tests** (new test class in `SEBT.Portal.StatePlugins.CO.Tests`):

- Seeds manifest entries into cache on first `GetResponseForPhone` call
- Returns correct household JSON for a known phone number
- Returns empty success response for an unknown phone number
- `ApplyPatchAsync` mutates address fields on the correct student
- `ApplyPatchAsync` mutates guardian fields on the correct student
- `ApplyPatchAsync` returns 404-shaped response for unknown `sebtChldId`
- Subsequent `GetResponseForPhone` reflects PATCH mutations

**MockCbmsHttpHandler tests** (update existing tests):

- `get-account-details` routes to data store and returns correct household
- `update-std-dtls` PATCH routes to data store
- Token/ping/check-enrollment still work

**ColoradoSummerEbtCaseService integration tests** (update existing):

- Mock mode with specific phone returns expected household shape
- Mock mode with unknown phone returns null (empty success → no students → null)

**Plugin DI bridge tests** (new test in main repo):

- Assembly scanning discovers plugin types correctly
- Factory registration logs on first construction
- `IStateHealthCheckService` plugin is eagerly instantiated and configures health checks
- Non-health-check plugins receive DI services via constructor injection

### Future cleanup (out of scope)

- **Remove `System.Composition` dependency:** Once the plugin DI bridge is stable, extract the assembly loading logic into a standalone helper class and remove all MEF references. The `[Export]`, `[ExportMetadata]`, and `[ImportingConstructor]` attributes on plugins can be removed at that time. This will be noted with code comments in the implementation.
- **Manifest auto-generation tooling:** A script or build step that regenerates `mock-manifest.json` from the household files whenever test data changes.
- **`reqNewCard` simulation:** Decide whether PATCH with `reqNewCard: "Y"` should mutate card status fields or just return success.
