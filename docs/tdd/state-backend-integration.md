# State Backend Integration — REST API Contract

## Problem Statement

The portal integrates with state case-management backends via MEF (System.Composition) plugins — compiled C# DLLs loaded at startup from `plugins-{state}/`. This works for DC and CO but has meaningful costs as we approach broader state onboarding:

- **Onboarding requires CFA involvement.** State CMS teams and their vendors cannot implement a C# plugin. Every new state integration requires CFA to author, package, and distribute a connector DLL.
- **The integration contract encodes CFA's stack.** The plugin interface (`ISummerEbtCaseService`) is expressed in C#. A state that has a working REST API cannot use it without a C# wrapper.
- **The contract has accreted DC-shaped abstractions.** Co-loading methods were added to the universal interface with docstrings saying "DC only; other states return false." CO is planning to implement co-loading via a different matching path. The abstraction doesn't generalize.
- **Infrastructure build coupling.** Email templates and blocked-address lists are embedded as assembly resources in the shared Infrastructure project, requiring a rebuild to add a new state.

The goal is a REST API spec that any state backend can implement — whether by a state's CMS vendor, a CFA-built middleware microservice, or a future state's in-house team — without requiring CFA code.

---

## Design Decisions

### Ports-and-adapters at the integration boundary

The portal's internal code never calls state backends directly. It calls C# interfaces (the "port"). Two adapters fulfill those interfaces:

1. **Plugin adapter** — MEF loads a compiled DLL from `plugins-{state}/`. Existing DC and CO connectors continue to work here, refactored to the new interfaces.
2. **REST adapter** — the default implementation; makes HTTP calls to a state backend that implements our OpenAPI spec.

At startup, MEF scans for a plugin. If found, use it. If not, fall back to the REST adapter, which reads `StateBackend:BaseUrl` from `appsettings.{state}.json`. DC and CO keep their plugins until they choose to migrate; new states get the REST path from day one.

This means the C# interfaces remain stable as the portal's internal abstraction. The OpenAPI spec is the *external* contract — what states implement. The two are related but distinct.

```
Portal (BFF)
  └─ C# interfaces (port)
       ├─ Plugin adapter  →  DC / CO connector DLLs (MEF, existing path)
       └─ REST adapter    →  State-hosted REST endpoint
                               ├─ Native CMS implementation
                               └─ SEBT middleware microservice (if needed)
```

### The optional middleware layer

Not all states will be able to implement the full spec natively. When needed, CFA builds a thin .NET microservice as an anticorruption layer: it translates the state's existing CMS API into our spec. It has no database. It deploys on state-owned SEBT portal infra (Docker Compose alongside portal components) — not CFA-managed infra. It is a last resort, not the default.

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

public record IdentitySignal(IdentitySignalType Type, string Value);

public enum IdentitySignalType
{
    DateOfBirth,
    StateBenefitId,
    FederalBenefitId,
    StateIssuedId,
    PhoneNumber,
}
```

DC passes `[StateBenefitId, DateOfBirth]`. CO might pass `[StateIssuedId, DateOfBirth]`. A third state could pass `[PhoneNumber, DateOfBirth]`. The interface doesn't change; the backend uses what it knows.

This is an *optional capability interface*. States that support co-loading export it via MEF (plugin path) or implement `/households/identify` in their REST endpoint. The portal checks for the capability before attempting a match; if absent, it skips the co-loaded path entirely.

### Capability discovery drives `allowedActions` (combined with feature flags)

The state backend publishes what it supports via `GET /capabilities`. The portal's `ISelfServiceEvaluator` combines three inputs to produce `allowedActions`:

```
allowedAction(X) =
    stateCapabilities.Supports(X)       // what the backend can do
    && SelfServiceRulesSettings[X]       // program timing, business rules
    && userMeetsRequirements(X)          // IAL, co-loading status, cooldown
```

A state that supports card replacement can still have it disabled portal-wide via `SelfServiceRulesSettings` (e.g., program not yet open). The backend capability is a necessary but not sufficient condition. This extends the existing `SelfServiceEvaluator` pattern rather than replacing it — capabilities feed in as a new input to the existing evaluator.

The REST adapter caches `GET /capabilities` at startup (or on a configurable TTL). Feature flag changes take effect on the next request via `IOptionsMonitor`, same as today.

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
| `GET` | `/households` | Lookup household by identifier type + value |
| `POST` | `/households/identify` | Co-loading identity match (flexible signal list) |
| `GET` | `/households/{householdId}/cases` | Cases / benefit records for a household |
| `PATCH` | `/households/{householdId}/address` | Update mailing address |
| `POST` | `/households/{householdId}/cases/{caseId}/card-replacement` | Request card replacement |
| `GET` | `/households/{householdId}/cases/{caseId}/card` | EBT card details |
| `GET` | `/enrollment` | Enrollment eligibility check |

**Error format:** ProblemDetails (RFC 9457) throughout, consistent with the portal's existing error shape. The REST adapter normalizes non-ProblemDetails error responses from backends that can't conform.

**Versioning:** URL prefix (`/v1/`). See open items.

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

| Interface | Description |
|---|---|
| `IStateHouseholdService` | Replaces `ISummerEbtCaseService`; household lookup and cases |
| `ICoLoadedIdentityService` | Optional capability; signal-based co-loading match |
| `IStateCapabilityService` | Capability discovery |
| `IStateBackendAuthStrategy` | Auth strategy abstraction |

Existing interfaces (`IAddressUpdateService`, `ICardReplacementService`, `IEnrollmentCheckService`) are cleaned up (DC/CO doc references removed, optional fields documented) but otherwise unchanged in Phase 1.

---

## Migration Plan

**Phase 1 — Foundation (no DC/CO connector changes)**
- Author `docs/openapi.yaml` and set up Redoc on GitHub Pages
- Implement new C# interfaces and `HouseholdLookupContext`
- Implement `RestStateBackendAdapter` (HTTP implementation of all interfaces)
- Implement auth strategy infrastructure (OAuth2 + ApiKey)
- Extend `ISelfServiceEvaluator` to consume `IStateCapabilityService`
- Wire new interfaces into portal use cases; existing plugins still load and win

**Phase 2 — DC/CO interface cleanup**
- Refactor DC and CO connector DLLs to implement the new C# interfaces
- Replace DC-specific co-loading methods with `ICoLoadedIdentityService`
- Plugins still load via MEF; no REST endpoint required

**Phase 3 — Reference REST implementation**
- Validate the REST adapter against a real state backend (likely CO, timed with their co-loading work)
- Build and document the middleware microservice template if needed

**Phase 4 — New state onboarding via REST**
- New states implement `docs/openapi.yaml` (or CFA builds middleware)
- No plugin DLL required; REST adapter is the default path

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
| OI-1 | CO co-loading: which `IdentitySignalType` values does CO plan to use? | Drives which signal types ship in v1 of the spec |
| OI-2 | Pagination strategy for `/cases` list endpoints: cursor-based or offset? | Spec design |
| OI-3 | Does the REST adapter normalize non-ProblemDetails error responses, or do we require conformance? | Middleware complexity |
| OI-4 | Circuit breaking / retry policy for state backend HTTP calls (Polly)? | Reliability |
| OI-5 | `GET /capabilities` caching: startup-time snapshot vs. TTL-based refresh? Cache invalidation when a backend redeploys? | Consistency |
| OI-6 | URL versioning (`/v1/`) vs. `Accept` header versioning? | Onboarding documentation |
