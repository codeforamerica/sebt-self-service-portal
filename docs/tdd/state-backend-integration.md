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

States may not be able to implement the full spec precisely. If/when needed, CFA builds a thin microservice as an anticorruption layer: it calls the state's existing CMS API and exposes the SEBT REST spec outward. It has no database. It deploys on state-owned SEBT portal infra (separate container alongside core portal components) — not CFA-managed infra. It is a last resort, not the default.

Middleware lives in this repo under `src/` (consistent with the monorepo direction). Each state that needs one gets its own project (e.g., `src/SEBT.StateMiddleware.CO/`). The CO proof-of-concept (see Phase 1) is the first instance and serves as the reference implementation template.

Middleware handles auth on both sides: inbound OAuth2/API key validation from the portal; outbound auth to the state's CMS using whatever the CMS requires. It is a full re-implementation against the CMS API — not a wrapper of the existing connector DLL. This produces a self-contained, readable reference that state CMS vendors can use when implementing their own conformant endpoints.

### Co-loading as a signal-based identity match

DC's co-loading methods prescribe a specific matching algorithm: benefit identifier + date of birth against DC's warehouse stored procedure. CO's planned co-loading may use different identifiers (e.g., a MyColorado-confirmed DOB with a different benefit identifier type).

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
    public const string DateOfBirth      = "date_of_birth";
    public const string StateBenefitId   = "state_benefit_id";
    public const string FederalBenefitId = "federal_benefit_id";
    public const string StateIssuedId    = "state_issued_id";
    public const string PhoneNumber      = "phone_number";
}
```

DC passes `[StateBenefitId, DateOfBirth]`. CO might pass `[StateIssuedId, DateOfBirth]`. A third state could pass `[PhoneNumber, DateOfBirth]` or a signal type not listed above — the portal passes it through without needing a code change.

This is an _optional capability_. States that support co-loading advertise `cases.coLoadedLookup` in their capabilities response. The portal checks for the capability before attempting a match; if absent, it skips the co-loaded path entirely. On the REST path, co-loading is a second call to `POST /cases/lookup` with a different signal set — not a separate endpoint.

**The `intent` field** distinguishes lookup types at the protocol level. It is an **open string constant** (same extensibility model as signal types): well-known values are `"primary"` and `"coLoad"`, but future values are possible (e.g., caseworker-assisted lookup). Backends MUST return `400` for unrecognized intent values rather than guessing — fail closed, since `intent` shapes access-control decisions. The portal passes a known value on every request.

- `"primary"` — the authenticated user's own household
- `"coLoad"` — a cross-household search (find a SNAP/TANF household to co-load); backends may apply stricter access rules

Enrollment eligibility check (`POST /enrollment/check`) is a separate endpoint with its own security posture (unauthenticated by default) and does not use `intent`.

### Capability discovery drives `allowedActions` (combined with feature flags)

The state backend publishes what it supports via `GET /capabilities`. The portal's `ISelfServiceEvaluator` combines three inputs to produce `allowedActions`:

```
allowedAction(X) =
    stateCapabilities.Supports(X)       // what the backend can do
    && SelfServiceRulesSettings[X]       // program timing, business rules
    && userMeetsRequirements(X)          // IAL, co-loading status, cooldown
```

Full capabilities response shape:

```json
{
  "specVersion": "1.0.0",
  "serviceMode": {
    "mode": "full",
    "until": null
  },
  "capabilities": {
    "cases": {
      "coLoadedLookup": { "supported": true },
      "cardDetails":    { "supported": true, "modes": ["batch"] },
      "cardReplacement": { "supported": true },
      "addressUpdate":   { "supported": true },
      "cardActivation":  { "supported": false }
    },
    "enrollment": {
      "check": { "supported": true }
    },
    "userAssertion": {
      "supported": true,
      "required": {
        "default":         false,
        "cardReplacement": true,
        "addressUpdate":   true
      }
    }
  }
}
```

Capability values are structured objects (`{ "supported": bool, ... }`) rather than bare booleans, so future metadata fields (`requiresMinIal`, `cooldownPeriodDays`, etc.) can be added without breaking existing consumers. The portal ignores unknown fields within a capability object (open/closed).

**`specVersion`** — declares which version of the SEBT spec this backend conforms to.

- The portal sends its own supported spec version on every request via `X-Sebt-Spec-Version: 1.0.0`.
- Within a major version: unknown capability keys and fields are ignored; absent capability = unsupported. New capabilities may only be *added*, never repurposed.
- Major version mismatch between portal and backend is fatal: the portal refuses to activate the backend, logs the mismatch, and degrades to a service-unavailable state. This prevents silent behavioral drift across incompatible versions.

**`serviceMode`** — runtime operational status, distinct from static capabilities.

| Mode | Portal behavior |
|---|---|
| `"full"` (default, may be absent) | Normal operation |
| `"readOnly"` | Suppress all write `allowedActions` (card replacement, address update); display `until` time to users |
| `"maintenance"` | All operations suspended; portal shows outage page; use `until` to indicate expected recovery time |

Backends enter `readOnly` during nightly batch windows or CMS maintenance where writes fail but reads succeed. `until` is an RFC 3339 timestamp or `null` if the window end is unknown.

**Per-case capability overrides (reserved, not yet implemented)** — capabilities are currently state-wide. A future version may allow individual cases returned from `POST /cases/lookup` to carry a `capabilities` object that narrows the state-wide defaults. Absent per-case capabilities = inherit state-wide defaults. This field is reserved in the spec from v1 to ensure it can be added non-breakingly when multi-CMS state deployments require it.

**Caching and invalidation:**

Capabilities are cached with a configurable TTL (default: 5 minutes). If the response includes `Cache-Control: max-age`, that value wins — backends should set a short `max-age` ahead of planned capability changes, and `no-cache` during a cutover window.

For near-real-time invalidation, backends include an `X-Sebt-Capabilities-ETag` header on every data response (lookup, writes). When the portal observes a changed ETag value compared to what it cached, it immediately re-fetches `/capabilities`. This lets a backend piggyback "my capabilities changed" onto the next request the user already makes — no push infrastructure required. The portal closes the stale-`allowedActions` window to a single request rather than a full TTL cycle.

**Conflict resolution:**
- Capabilities says `supported: false`, endpoint returns `200`: log warning, proceed (cache may be stale).
- Capabilities says `supported: true`, endpoint returns `501`: log error (backend misconfiguration), disable feature for session, do not retry.

### End-user assertion (optional)

By default, state backends trust the portal's service credential (OAuth2 client credentials or API key) and rely on the portal to have validated and authorized the end user. This is the baseline and is sufficient for most states.

For states that want finer-grained access control at the data layer, the portal includes an `X-Sebt-User-Identity` header on every **authenticated** data-access request. State backends that validate it can scope results to the authenticated user; those that don't can ignore it.

**Important:** Enrollment check (`POST /enrollment/check`) is unauthenticated by default. The portal does not send the assertion header for enrollment flows, and state backends MUST NOT mark `enrollmentCheck` as `required: true` in the `userAssertion` capability.

Header value: a short-lived signed JWT with no PII:

```json
{
  "ial": 2,
  "userRef": "<hmac-sha256-of-portal-user-id>",
  "iat": 1719878400,
  "exp": 1719878460
}
```

- `ial`: the authenticated user's identity assurance level (1 or 2)
- `userRef`: a stable, opaque, non-reversible reference (HMAC-SHA256 of the portal's user ID with a per-state secret)
- 60-second TTL prevents replay

Signed by the portal using `StateBackend:UserAssertionSigningKey` from config.

**`userAssertion.required`** is a per-operation map with a `default` key. The portal reads `required[operation] ?? required.default`. Example: require assertion for writes but not reads:

```json
"userAssertion": {
  "supported": true,
  "required": {
    "default":         false,
    "cardReplacement": true,
    "addressUpdate":   true
  }
}
```

When assertion validation fails: `403` with ProblemDetails; include `requiredIal` in extensions if the failure is IAL-insufficient.

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
  },
  "UserAssertionSigningKey": "<secret-key-name>"
}
```

Adding support for a new auth mechanism is a new class implementing `IStateBackendAuthStrategy` and a keyed registration in `Dependencies.cs`. No portal code changes.

### Adapter selection is config-driven, not file-presence-driven

If `StateBackend:BaseUrl` is present in `appsettings.{state}.json`, the REST adapter is used. If it is absent, the portal looks for a plugin DLL in `plugins-{state}/`. The REST adapter is therefore the intended default for new states; the plugin path is the backward-compat path for DC and CO during migration.

Migrating a state from plugin to REST = adding `StateBackend:BaseUrl` to its config. No DLL removal required (the config presence takes precedence), though the DLL can be removed once confidence is established.

This is deliberately asymmetric: a state cannot accidentally get the plugin path by having a DLL in the directory if REST is configured. The new integration pattern wins explicitly.

### State backends are case-centric; household grouping is the adapter's job

State backends don't have a stable portal household ID. They have cases (one per child) indexed by natural keys — guardian email, phone, state-issued benefit IDs. The "household" concept (cases grouped under one guardian) is a portal-side abstraction that doesn't exist natively in most state CMS systems.

The REST adapter is responsible for that grouping. When it calls `POST /cases/lookup`, the signal set used to find those cases defines the household — all returned cases belong to the same household by virtue of matching the same identity. The adapter builds the portal's `Household` model from the flat case list and caches it for the session. No household ID circulates in the state backend API.

Consequences:

- **No path-based household reference** in the external contract. Operations that apply to all cases in a household (address update) receive explicit case IDs in the request body. Operations that apply to a single case (card replacement) use the state's native case ID, which is returned from `/cases/lookup`.
- **Address update is a batch operation.** `POST /cases/address-updates` with `{ "caseIds": [...], "address": {...} }`. The operation is semantically idempotent — applying the same address to the same cases twice yields the same result, so Polly can safely retry. Response is 207 Multi-Status with a per-case result entry; the portal retries only the failed cases.
- **No pagination.** Household sizes in this program are bounded by family structure and program rules. State backends return all cases in a single response. The spec documents a reasonable upper bound (e.g., 20).

### Card details loading is capability-driven

State backends vary in how they can serve EBT card details. Some (notably CO) load details for all cases in a single CMS round-trip; others can serve them one case at a time; some may support both. The `cardDetails.modes` array reflects this — extensible to future delivery modes (e.g., `"stream"`) without new sibling booleans:

- `"batch"` — supports `includeCardDetails: true` on `POST /cases/lookup`; card details arrive inline.
- `"perCase"` — supports `GET /cases/{caseId}/card`; card details fetched per case on demand.

The REST adapter consults capabilities at runtime to pick the loading strategy:

| modes contains `"batch"` | modes contains `"perCase"` | Adapter behavior |
|---|---|---|
| ✓ | — | `includeCardDetails: true` on lookup; details inline |
| — | ✓ | Parallel `GET /cases/{caseId}/card` after initial lookup |
| ✓ | ✓ | Prefer batch |
| — | — | Card management features disabled for this state |

The portal only requests card details on pages that need them (card management flows), not on the dashboard. `IncludeCardDetails` in `HouseholdLookupContext` controls whether the adapter fetches card details at all; `cardDetails.modes` controls how.

**Unsupported optional endpoints must return 501.** A state backend that doesn't support per-case loading must still expose `GET /cases/{caseId}/card` and return `501 Not Implemented` with a ProblemDetails body. This gives the REST adapter an unambiguous signal if capabilities are misconfigured, surfacing the mismatch as a logged error rather than a cryptic 404 or 500 mid-flow. The same rule applies to any optional endpoint in the spec. `501` is explicitly excluded from Polly retry and circuit-breaker classification — it is not a transient failure.

### `HouseholdLookupContext` replaces ad-hoc parameters

The current `ISummerEbtCaseService` lookup methods have accumulated DC-specific parameters (`Guid? portalUserId` for DC warehouse correlation) and CO-specific parameters (`bool includeCardService` for CBMS two-phase fetching). Both callers that don't use a parameter pass `null` / `true` and move on.

These are replaced by an options object:

```csharp
public record HouseholdLookupContext
{
    public bool IncludeCardDetails { get; init; } = false;
    public string? CorrelationId { get; init; }
}
```

`CorrelationId` replaces `portalUserId` without encoding DC's warehouse concept. `IncludeCardDetails` replaces `includeCardService` without encoding CO's CBMS concept. New parameters go here rather than onto method signatures. States ignore fields they don't use.

---

## REST API Contract (summary)

The canonical contract is `docs/openapi.yaml` (see Phase 1 deliverables). The table below is a map, not the spec.

| Method | Path | Description |
|---|---|---|
| `GET` | `/health` | Readiness check (includes CMS reachability); unauthenticated |
| `GET` | `/capabilities` | Spec version, service mode, and capability discovery |
| `POST` | `/cases/lookup` | Find cases by identity signals; `intent` string; optional `includeCardDetails` for batch card data |
| `GET` | `/cases/{caseId}/card` | EBT card details for a single case (requires `cardDetails.modes: ["perCase"]`; return `501` if not supported) |
| `POST` | `/cases/address-updates` | Batch address update; semantically idempotent; returns 207 Multi-Status |
| `POST` | `/cases/{caseId}/card-replacement` | Card replacement; requires `Idempotency-Key` header |
| `POST` | `/enrollment/check` | Enrollment eligibility check; signals in body; unauthenticated |

`{caseId}` is the state backend's native case identifier, returned in each case object from `POST /cases/lookup`. The portal treats it as opaque.

### Key request/response shapes

**`POST /cases/lookup` request:**
```json
{
  "signals": [
    { "type": "email", "value": "guardian@example.com", "verified": true, "source": "portal" }
  ],
  "intent": "primary",
  "includeCardDetails": false
}
```
- Signals are AND-matched where possible; the backend uses the most specific combination available.
- Signal objects may carry optional sibling fields beyond `type` and `value` (e.g., `verified`, `source`). Backends ignore unknown sibling fields.
- `intent` is an open string constant. Backends MUST return `400` for unrecognized values.
- No match: `200` with `{ "cases": [] }`.

**`POST /cases/address-updates` request / response:**
```json
{
  "caseIds": ["abc", "def"],
  "address": {
    "street1": "123 Main St",
    "city": "Denver",
    "state": "CO",
    "zip": "80203"
  }
}
```
```json
{
  "results": [
    { "caseId": "abc", "status": 200 },
    { "caseId": "def", "status": 422, "error": { "title": "Address validation failed", "detail": "..." } }
  ]
}
```

**`POST /cases/{caseId}/card-replacement`:** requires `Idempotency-Key: <uuid>` header. Backend deduplicates within 24 hours — replayed key returns the original response. Missing key: `400 Bad Request`. This endpoint is excluded from automatic Polly retry; the portal manages retry manually after verifying idempotency key reuse is safe.

**`POST /enrollment/check` request:**
```json
{
  "signals": [
    { "type": "state_benefit_id", "value": "SNAP-12345" }
  ]
}
```

### Error format

ProblemDetails (RFC 9457) throughout, consistent with the portal's existing error shape.

Optional endpoints that a state backend does not implement must return `501 Not Implemented` with a ProblemDetails body — not `404`. The REST adapter treats `501` as "capability not available," the same as `capabilities: { "supported": false }`. `501` is excluded from retry and circuit-breaker logic.

The REST adapter normalizes non-ProblemDetails error responses from backends that cannot fully conform.

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
- **Retry with exponential backoff + jitter** — 3 attempts on transient failures: `5xx` (excluding `501`), network errors, and timeouts. `POST /cases/{caseId}/card-replacement` is **excluded from automatic retry** — callers provide an `Idempotency-Key` and manage retry manually to avoid submitting a duplicate key without explicit intent.
- **Circuit breaker** — trips on `5xx` (excluding `501`) and timeouts after a configurable threshold (default: 5 failures in 30s); half-open probe after a configurable recovery window (default: 60s). `429` responses do not trip the circuit breaker.
- **429 / Retry-After** — if the backend returns `429`, the adapter honors the `Retry-After` header before the next attempt. This applies outside the standard retry pipeline.

All thresholds are configurable under `StateBackend:Resilience`. Circuit-open state surfaces to the frontend as a service-unavailable response without leaking backend details.

### Caching in the REST adapter

The portal already uses `HybridCache` (Microsoft.Extensions.Caching.Hybrid, .NET 9+), which provides L1 in-process and optional L2 distributed caching behind a single API. The REST adapter uses the same infrastructure.

Three separately TTL'd entries:

- **Household data** (cases without card details) — 30s TTL. Cache key: `portalUserId`. The authenticated user's portal ID is strictly 1:1 with the identity-signal set used for lookup within a session.
- **Card details** — 60s TTL. Cache key: `portalUserId:cards`. Cached separately so dashboard loads don't pay the card-detail fetch cost.
- **Capabilities** — 5min TTL (or `Cache-Control: max-age` from the response, which wins).

**ETag-based capability invalidation:** the backend includes an `X-Sebt-Capabilities-ETag` header on every data response (lookup, writes). When the portal observes a changed ETag value compared to what it has cached, it immediately re-fetches `/capabilities`. This closes the stale-`allowedActions` window to a single request rather than a full TTL cycle — no push infrastructure required.

Write invalidation: the adapter maintains a session-scoped `caseId → portalUserId` mapping (populated during lookup). When a case-scoped write (address update, card replacement) occurs, the adapter uses this mapping to invalidate both the household and card entries for the correct user.

L2 (distributed) caching is optional — if Redis is not configured, `HybridCache` falls back to L1 only. This keeps single-instance deployments simple while supporting multi-replica deployments without code changes.

---

## Conventions

All state backends must conform to these field-level rules. The OpenAPI spec will enforce these as schema constraints.

| Concern | Rule |
|---|---|
| Content-Type | `application/json` on all requests and responses |
| Date-times | RFC 3339 with UTC offset (`2024-06-15T14:30:00Z`) |
| Date-only | `YYYY-MM-DD` (used for `date_of_birth`) |
| Phone numbers | E.164 (`+13035551234`) |
| Benefit / monetary amounts | Integer cents; no decimal |
| JSON field casing | camelCase |
| Null vs absent | Absent = field not applicable. `null` = applicable but no value. Prefer absent. |
| Unknown signal types | Backends ignore silently; never error on unrecognized type |
| Unknown signal fields | Backends ignore sibling fields beyond `type` and `value` |
| Unknown capability keys | Portal ignores; absent = unsupported |
| Unknown `intent` values | Backends return `400`; fail closed |

**Signal value formats by known type:**

| Signal type | Format |
|---|---|
| `date_of_birth` | `YYYY-MM-DD` |
| `phone_number` | E.164 |
| `state_benefit_id` | Opaque string; normalized (trimmed, dashes and spaces stripped) |
| `federal_benefit_id` | Opaque string; normalized |
| `state_issued_id` | Opaque string; normalized |

---

## Data Flow

```
appsettings.{state}.json
  StateBackend:BaseUrl + Auth     →  REST adapter (IHttpClientFactory)
                                         ↓
                                   GET /capabilities (cached, 5min TTL)
                                         ↓ (ETag on subsequent responses triggers re-fetch)
                             IStateCapabilityService.GetCapabilitiesAsync()
                                         ↓
SelfServiceRulesSettings             ISelfServiceEvaluator
(IOptionsMonitor, hot-reload)    ←         |
User context (IAL, co-loading)   ←         |
serviceMode check                ←         |
                                         ↓
                                     AllowedActions
                                         ↓
                               API response → frontend
```

Co-loading lookup path:

```
User supplies identity signals (from UI)
  → ICoLoadedIdentityService.TryMatchAsync(signals)
       ├─ Plugin path: DLL method call
       └─ REST path:   POST /cases/lookup  (intent: "coLoad", co-loading signals)
  → CoLoadedIdentityMatchResult { Matched, Cases[] }
  → Adapter merges co-loaded cases into portal's Household model
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
- **CO proof-of-concept middleware** (`src/SEBT.StateMiddleware.CO/`) — a full re-implementation of CO's CBMS integration that exposes the SEBT REST spec, written from scratch against the spec (not a wrapper of the existing CO connector DLL). Does not need to deploy to production; its purpose is to validate the architecture, stress-test the spec shape against a real integration, and produce a reference implementation that state CMS vendors can read. Becomes the template for future middleware projects in this repo.

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
| OI-3 | Rate-limit advertisement: should `/capabilities` include a `limits` hint (e.g., `lookupPerSecond`) so the portal can self-pace rather than discover limits reactively via `429`? | Nice-to-have; deferred |
| OI-4 | Consent/authorization state: some states may gate data release on an explicit consent record. No structured representation currently; would need a `consentRequired` capability + defined `403`-with-`consentUrl` ProblemDetails shape | Known gap; deferred |
