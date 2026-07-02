# State Backend Integration — REST API Contract

## Problem Statement

The portal integrates with state case-management backends via MEF (System.Composition) plugins — compiled C# DLLs loaded at startup from `plugins-{state}/`. This works for DC and CO but has meaningful costs as we approach broader state onboarding:

- **Onboarding requires CFA involvement.** It is likely impractical for state staff and/or their vendors to implement a C# plugin. Every new state integration requires CFA to author, package, and distribute a connector DLL.
- **The integration contract encodes CFA's stack.** The plugin interface (`ISummerEbtCaseService`) is expressed in C#. A state that has a working REST API cannot use it without a C# wrapper.
- **The contract has accreted DC-shaped abstractions.** Co-loading methods were added to the universal interface with docstrings saying "DC only; other states return false." CO may be planning to implement co-loading via a different matching path. The abstraction doesn't generalize.
- **Infrastructure build coupling.** Email templates and blocked-address lists are embedded as assembly resources in the shared Infrastructure project, requiring a rebuild to add a new state.

The goal is a REST API spec that any state backend can implement — whether by a state's CMS vendor, a CFA-built middleware microservice, or a future state's in-house team — without requiring CFA code.

---

## Design Decisions

### Ports-and-adapters at the integration boundary

The portal's core business logic code ("use cases") never calls state backends directly. It calls C# interfaces (the "port"). Two adapters fulfill those interfaces:

1. **Plugin adapter** — The portal dynamically loads a compiled DLL from `plugins-{state}/`. Existing DC and CO connectors continue to work here, refactored to the new interfaces.
2. **REST adapter** — the default implementation; makes HTTP calls to a state backend that implements our OpenAPI spec.

At startup, the portal scans for a plugin. If found, use it. If not, fall back to the REST adapter, which reads `StateBackend:BaseUrl` from `appsettings.{state}.json`. DC and CO keep their plugins until they choose to migrate; new states get the REST path from day one.

This means the C# interfaces remain stable as the portal's internal abstraction. The OpenAPI spec is the _external_ contract — what states implement. The two are related but distinct.

```
Portal (BFF)
  └─ C# interfaces (port)
       ├─ Plugin adapter  →  DC / CO connector DLLs (MEF, existing path)
       └─ REST adapter    →  State-hosted REST endpoint
                               ├─ Native CMS implementation
                               └─ SEBT middleware microservice (if needed)
```

### The optional middleware layer

States may not be able to implement the full spec precisely. If/when needed, CFA builds a thin microservice as an anticorruption layer: it translates the state's existing CMS API into our spec. It has no database. It deploys on state-owned SEBT portal infra (separate container sitting alongside core portal components) — not CFA-managed infra. It is a last resort, not the default.

### Co-loading as a signal-based identity match

DC's co-loading methods (`TryMatchCoLoadedGuardianByBenefitIdAndDobAsync`, `GetHouseholdByBenefitIdentifierAndDobAsync`) prescribe a specific matching algorithm: benefit identifier + date of birth against DC's warehouse stored procedure. CO's planned co-loading may use different identifiers (e.g., a MyColorado-confirmed DOB with a different benefit identifier type).

Rather than adding a second parallel method for CO, the interface is redesigned at a higher abstraction level: "given a set of identity signals, find a co-loaded household." The state backend decides which subset of signals it can use to produce a match. The portal passes everything it has; the backend uses what applies.

```csharp
public interface ICoLoadedIdentityService
{
    Task<CoLoadedIdentityMatchResult> TryMatchAsync(
        IReadOnlyList<IdentitySignal> signals,
        CancellationToken ct);
}

public record IdentitySignal(string Type, string Value);

/// <summary>
/// Well-known signal type identifiers. Not an enum — new types can be added
/// by extending this class or passing custom strings without a portal code change.
/// State backends ignore types they don't recognize.
/// </summary>
public static class IdentitySignalType
{
    public const string DateOfBirth     = "date_of_birth";
    public const string StateBenefitId  = "state_benefit_id";
    public const string FederalBenefitId = "federal_benefit_id";
    public const string StateIssuedId   = "state_issued_id";
    public const string PhoneNumber     = "phone_number";
}
```

DC passes `[StateBenefitId, DateOfBirth]`. CO might pass `[StateIssuedId, DateOfBirth]`. A third state could pass `[PhoneNumber, DateOfBirth]` or a signal type not listed above — the portal passes it through without needing a code change.

This is an _optional capability interface_. States that support co-loading export it via MEF (plugin path) or implement `POST /households/identify` in their REST endpoint. The portal checks for the capability before attempting a match; if absent, it skips the co-loaded path entirely.

### Capability discovery drives `allowedActions` (combined with feature flags)

The state backend publishes what it supports via `GET /capabilities`. The portal's `ISelfServiceEvaluator` combines three inputs to produce `allowedActions`:

```
allowedAction(X) =
    stateCapabilities.Supports(X)       // what the backend can do
    && SelfServiceRulesSettings[X]       // program timing, business rules
    && userMeetsRequirements(X)          // IAL, co-loading status, cooldown
```

A state that supports card replacement can still have it disabled portal-wide via `SelfServiceRulesSettings` (e.g., program not yet open). The backend capability is a necessary but not sufficient condition. This extends the existing `SelfServiceEvaluator` pattern rather than replacing it — capabilities feed in as a new input to the existing evaluator.

Capability values are structured objects rather than booleans, even though `supported: bool` is all the portal uses today:

```json
{
  "capabilities": {
    "household": {
      "resolve":                { "supported": true },
      "coLoadedIdentityMatch":  { "supported": true }
    },
    "cases": {
      "cardReplacement": { "supported": true },
      "addressUpdate":   { "supported": true },
      "cardActivation":  { "supported": false }
    },
    "enrollment": {
      "check": { "supported": true }
    }
  }
}
```

The portal ignores unknown fields within a capability object (open/closed). Future additions — `requiresMinIal`, `cooldownPeriodDays`, time-based availability windows — can be added to the spec and consumed by updated portal versions without breaking states that don't provide them.

**Caching:** The REST adapter caches `GET /capabilities` with a configurable TTL (default: 5 minutes). If the response includes a `Cache-Control: max-age` header, that value wins. This lets state backends that want finer control set their own TTL without requiring portal config changes. Feature flag changes take effect on the next request via `IOptionsMonitor`, same as today.

### Auth strategy pattern

Per-state auth is configured in `appsettings.{state}.json`. The REST adapter selects the strategy at HTTP client construction time.

```csharp
public interface IStateBackendAuthStrategy
{
    Task ApplyAsync(HttpRequestMessage request, CancellationToken ct);
}
```

Shipped implementations:

- `OAuth2ClientCredentialsAuthStrategy` — default; handles token caching and refresh. CO already uses this pattern.
- `ApiKeyAuthStrategy` — header-based; for states that cannot run an OAuth server.

Future (not in scope):

- `MutualTlsAuthStrategy` — client cert from the cert store.

The strategy is resolved by name via keyed DI. Config:

```json
"StateBackend": {
  "BaseUrl": "https://state-backend.example.gov/sebt/v1",
  "Auth": {
    "Strategy": "OAuth2ClientCredentials",
    "TokenEndpoint": "https://auth.example.gov/token",
    "ClientId": "sebt-portal",
    "ClientSecretKeyName": "state-backend-client-secret"
  }
}
```

Adding support for a new auth mechanism is a new class implementing `IStateBackendAuthStrategy` and a keyed registration in `Dependencies.cs`. No portal code changes.

### Adapter selection is config-driven, not file-presence-driven

If `StateBackend:BaseUrl` is present in `appsettings.{state}.json`, the REST adapter is used. If it is absent, the portal looks for a plugin DLL in `plugins-{state}/`. The REST adapter is therefore the intended default for new states; the plugin path is the backward-compat path for DC and CO during migration.

Migrating a state from plugin to REST = adding `StateBackend:BaseUrl` to its config. No DLL removal required (the config presence takes precedence), though the DLL can be removed once confidence is established.

This is deliberately asymmetric: a state cannot accidentally get the plugin path by having a DLL in the directory if REST is configured. The new integration pattern wins explicitly.

### Household resolution uses opaque state-native references

State backends don't have a concept of a "portal household ID." Resolution always starts from natural key(s) — email, phone, state-issued ID — and returns a `stateReferenceId` that is opaque to the portal. The portal treats it as a session-scoped handle for subsequent operations. The state backend interprets it however it needs to (a primary key, a token, a compound key encoded as a string).

`POST /households/resolve` is the resolution entry point. It accepts a list of identifier signals (same structure as the co-loading identify endpoint) and returns a `HouseholdData` object including `stateReferenceId`. Subsequent operations — address update, card replacement, card details — use `{stateReferenceId}` in the path. The portal never constructs or inspects this value; it only stores and forwards it.

Cases within a household similarly carry a `caseReferenceId` returned from `/households/{ref}/cases`. Case-level operations use that reference.

There is no pagination for `/cases`. Household sizes in this program are bounded by family structure and program rules. State backends must return all cases in a single response. The spec will document a reasonable upper bound (e.g., 20) and note that the portal will not request pages.

### `HouseholdLookupContext` replaces ad-hoc parameters

The current `ISummerEbtCaseService` lookup methods have accumulated DC-specific parameters (`Guid? portalUserId` for DC warehouse correlation) and CO-specific parameters (`bool includeCardService` for CBMS two-phase fetching). Both callers that don't use a parameter pass `null` / `true` and move on.

These are replaced by an options object:

```csharp
public record HouseholdLookupContext
{
    public bool IncludeCardDetails { get; init; } = true;
    public string? CorrelationId { get; init; }
}
```

`CorrelationId` replaces `portalUserId` without encoding DC's warehouse concept. `IncludeCardDetails` replaces `includeCardService` without encoding CO's CBMS concept. New parameters go here rather than onto method signatures. States ignore fields they don't use.

---

## REST API Contract (summary)

The canonical contract is `docs/openapi.yaml` (see Phase 1 deliverables). The table below is a map, not the spec.

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Liveness / readiness |
| `GET` | `/capabilities` | Capability discovery |
| `POST` | `/households/resolve` | Resolve a household from identifier signals; returns `HouseholdData` with `stateReferenceId` |
| `POST` | `/households/identify` | Co-loading identity match; returns match result + `stateReferenceId` if matched |
| `GET` | `/households/{ref}/cases` | All cases for a resolved household (no pagination) |
| `PATCH` | `/households/{ref}/address` | Update mailing address |
| `POST` | `/households/{ref}/cases/{caseRef}/card-replacement` | Request card replacement |
| `GET` | `/households/{ref}/cases/{caseRef}/card` | EBT card details |
| `GET` | `/enrollment` | Enrollment eligibility check |

`{ref}` and `{caseRef}` are opaque `stateReferenceId` / `caseReferenceId` values returned from the resolution step. The portal does not construct or interpret them.

**Error format:** ProblemDetails (RFC 9457) throughout, consistent with the portal's existing error shape. The REST adapter normalizes non-ProblemDetails error responses from backends that can't fully conform.

### Versioning

URL prefix: all endpoints live under `/v1/`.

Alternatives considered:

| Approach | Tradeoff |
|---|---|
| URL prefix (`/v1/`) | Visible, cacheable, works with any HTTP client, easy to test with curl. Breaking change = new prefix; both versions coexist during transition. **Chosen.** |
| `Accept` header (`application/vnd.sebt.v1+json`) | More REST-pure; doesn't pollute the URL namespace. Harder to test, invisible in browser address bars, not supported by all API gateways. Not recommended for a government partner audience. |
| Query param (`?version=1`) | Simple but contaminates resource identity; considered poor practice. |
| Subdomain (`v1.api.example.gov`) | Requires DNS and TLS cert per version. Operationally expensive for state partners. |

### Fault tolerance

The REST adapter applies a Polly resilience pipeline per named HTTP client:

- **Timeout** — per-request deadline (default: 10s, configurable). Prevents slow backends from blocking portal request threads.
- **Retry with exponential backoff** — 3 attempts on transient failures (5xx, network errors, timeouts). Jitter applied to avoid thundering-herd on backend restarts.
- **Circuit breaker** — trips after a configurable failure threshold (default: 5 failures in 30s); half-open probe after a configurable recovery window (default: 60s). When open, fails fast rather than queuing requests against a downed backend.

All thresholds are configurable under `StateBackend:Resilience`. Circuit-open state surfaces to the frontend as a service-unavailable response without leaking backend details.

### Caching in the REST adapter

The REST adapter caches `HouseholdData` (household + cases) with a short configurable TTL (default: 30s). This matches the CO connector's existing caching behavior and avoids redundant round-trips within a single user session (e.g., loading the dashboard then immediately navigating to the card replacement flow).

Cache keys are scoped to `stateReferenceId`. Write operations (address update, card replacement) invalidate the cached entry for that reference.

Capabilities are cached separately with a longer TTL (default: 5 minutes). If the `/capabilities` response includes `Cache-Control: max-age`, that value takes precedence over the configured default. This lets state backends that want finer control set their own TTL without requiring portal config changes.

---

## Data Flow

```
appsettings.{state}.json
  StateBackend:BaseUrl + Auth     →  REST adapter (IHttpClientFactory)
                                         ↓
                                   GET /capabilities (cached at startup)
                                         ↓
                             IStateCapabilityService.GetCapabilitiesAsync()
                                         ↓
SelfServiceRulesSettings             ISelfServiceEvaluator
(IOptionsMonitor, hot-reload)    ←         |
User context (IAL, co-loading)   ←         |
                                         ↓
                                     AllowedActions
                                         ↓
                               API response → frontend
```

Co-loading identity match path:

```
User supplies identity signals (from UI)
  → ICoLoadedIdentityService.TryMatchAsync(signals)
       ├─ Plugin path: DLL method call
       └─ REST path:   POST /households/identify
  → CoLoadedIdentityMatchResult { Matched, HouseholdId? }
  → (existing co-loading enrollment flow continues)
```

---

## New C# Interfaces (Phase 1)

| Interface                   | Description                                                  |
| --------------------------- | ------------------------------------------------------------ |
| `IStateHouseholdService`    | Replaces `ISummerEbtCaseService`; household lookup and cases |
| `ICoLoadedIdentityService`  | Optional capability; signal-based co-loading match           |
| `IStateCapabilityService`   | Capability discovery                                         |
| `IStateBackendAuthStrategy` | Auth strategy abstraction                                    |

Existing interfaces (`IAddressUpdateService`, `ICardReplacementService`, `IEnrollmentCheckService`) are cleaned up (DC/CO doc references removed, optional fields documented) but otherwise unchanged in Phase 1.

---

## Migration Plan

**Phase 1 — Foundation (no DC/CO connector changes)**

- Author `docs/openapi.yaml` and set up Redoc on GitHub Pages
- Implement new C# interfaces and `HouseholdLookupContext`
- Implement `RestStateBackendAdapter` (HTTP implementation of all interfaces, with Polly resilience and caching)
- Implement auth strategy infrastructure (OAuth2 + ApiKey)
- Extend `ISelfServiceEvaluator` to consume `IStateCapabilityService`
- Wire new interfaces into portal use cases; existing plugins still load via the plugin path
- **CO proof-of-concept middleware** — build a thin .NET middleware microservice that wraps the CO connector's logic and exposes the SEBT REST spec. This does not need to be deployed to production; its purpose is to validate the architecture, stress-test the spec against a real integration, and build team + stakeholder confidence. The PoC becomes the template for future middleware layers.

**Phase 2 — DC/CO interface cleanup**

- Refactor DC and CO connector DLLs to implement the new C# interfaces
- Replace DC-specific co-loading methods with `ICoLoadedIdentityService`
- Plugins still load via MEF when `StateBackend:BaseUrl` is absent; no REST endpoint required

**Phase 3 — New state onboarding via REST**

- New states implement `docs/openapi.yaml` natively (or CFA builds middleware using the Phase 1 PoC as a template)
- No plugin DLL required; the REST adapter is the default path when `StateBackend:BaseUrl` is configured

**Phase 4 — DC/CO REST migration (optional, state-timeline-dependent)**

- DC and CO can cut over from their plugin DLLs to REST endpoints at their own pace
- Migration = setting `StateBackend:BaseUrl` in their config; the plugin DLL becomes inert and can be decommissioned

---

## OpenAPI Spec Rendering

The spec (`docs/openapi.yaml`) is rendered via Redoc deployed to GitHub Pages on push to `main`.

- `docs/index.html` — single file, loads Redoc from CDN, points at `openapi.yaml`
- GitHub Actions workflow deploys `docs/` to the `gh-pages` branch
- GitHub Pages access restricted to org members (private repo)
- Local development: 42Crunch OpenAPI Editor VS Code extension (live preview + validation)

---

## Open Items

| # | Question | Impact |
|---|---|---|
| OI-1 | CO co-loading: which `IdentitySignalType` values does CO plan to use? | Drives which signal types are documented as well-known in v1 of the spec |
| OI-2 | Default Polly thresholds (timeout, retry count, circuit breaker window) — validate against real CO connector latency profile before hardcoding | Resilience config defaults |
| OI-3 | Should the middleware microservice template live in this repo (as a `src/SEBT.StateMiddleware/` project) or a separate repo? | PoC scope |
| OI-4 | For the CO PoC middleware: does it wrap the existing CO connector DLL directly, or re-implement the CBMS calls from scratch? Wrapping is faster; re-implementing produces a cleaner template. | PoC build approach |
