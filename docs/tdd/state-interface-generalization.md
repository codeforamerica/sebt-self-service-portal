# State Interface Generalization — Portal-Side Consumption Design

**Status:** Draft — spike in progress (DC-568)

## Problem

The portal's plugin interfaces are too coupled to state-specific detail. `ISummerEbtCaseService` exposes a lookup method per identifier shape (`GetHouseholdByIdentifierAsync`, `GetHouseholdByGuardianEmailAsync`, `GetHouseholdByBenefitIdentifierAndDobAsync`) plus co-loading methods documented "DC only; other states return false." Adding a state means writing a new .NET plugin against these shapes. There's no configuration-first integration path.

This TDD covers the *portal side* of the fix: the C# interface the use-case layer calls, how the portal talks either REST or plugin behind that interface, and the adapter that keeps DC and CO working unchanged.

The REST contract itself ([`docs/openapi.yaml`](../openapi.yaml)) is designed separately. This document consumes it; it doesn't redefine it.

## Where we're going

The north star is *all-REST*: every state — including DC and CO — served by a REST backend, with plugins retired. For DC and CO that means a middleware microservice that adapts their existing APIs to our contract.

That's a goal, not a guarantee. Plugins may live a long time, especially for DC, where a middleware may never be prioritized. So we treat the plugin adapter as a durable compatibility layer we're willing to keep clean — not throwaway scaffolding we tolerate being ugly because it's "temporary."

Priorities, in order:

1. **New-state velocity is the point.** A new state implements the REST contract and configures a URL. No portal code change, no DLL.
2. **DC and CO keep working, unchanged.** Their connector plugins don't change, except one optional, scoped reshape (below).
3. **Keep the adapter clean, because it may outlast us.** The adapter carries the plugin-to-contract impedance mismatch. We don't tax new states or the use-case layer to keep plugins alive — and we don't let the adapter rot, since it may be load-bearing for years.

## Design principles

- **Operations mirror the REST contract; return types stay the portal's Core domain models.** The interface surface (lookup, card details, address update, card replacement, enrollment, capabilities, health) matches the REST endpoints one-to-one, so `RestStateBackendClient` is a thin HTTP + map. But it returns `HouseholdData` / `AddressUpdateResult` — the models the use-case layer already consumes — so handlers barely change and the adapter maps plugin→Core directly instead of plugin→DTO→Core.
- **One interface, not a per-capability split.** Capabilities are read at runtime, so splitting would force async I/O during DI registration for no real safety gain. An unsupported call fails at runtime regardless of how we shape the types; the existing `ISelfServiceEvaluator` gate already stops callers from getting that far.
- **The adapter stays mechanical.** No `if (state == "dc")` branching inside it. Where the adapter can't be mechanical, we reshape the plugin contract until it can.
- **Capabilities are derived, not asserted.** We compute them from what the connector actually registered, never from a hand-maintained per-state list.
- **Reuse the gate that already exists.** `ISelfServiceEvaluator` computes `allowedActions` and every write handler already checks it. Capability enforcement lives there. We don't invent a second gate.

## Proposed interface shapes

Sketches — enough to communicate intent. Final signatures land in the `sebt-self-service-portal-state-connector` repo.

```csharp
/// <summary>
/// Unified entry point for portal-to-state-backend interaction.
/// Both RestStateBackendClient and PluginAdapter implement this.
/// Returns the portal's Core domain models, not REST DTOs.
/// </summary>
public interface IStateBackendClient
{
    Task<StateCapabilities> GetCapabilitiesAsync(CancellationToken ct = default);
    Task<StateHealth>       GetHealthAsync(CancellationToken ct = default);

    Task<HouseholdData?> LookupHouseholdAsync(HouseholdLookupQuery query, CancellationToken ct = default);
    Task<CardDetails?>   GetCardDetailsAsync(string caseId, CancellationToken ct = default);

    Task<AddressUpdateResult>    UpdateAddressAsync(AddressUpdateRequest request, CancellationToken ct = default);
    Task<CardReplacementResult>  RequestCardReplacementAsync(string caseId, CardReplacementRequest request, string idempotencyKey, CancellationToken ct = default);
    Task<CardReplacementStatus?> GetCardReplacementStatusAsync(string caseId, string requestId, CancellationToken ct = default);

    Task<EnrollmentCheckResult> CheckEnrollmentAsync(EnrollmentCheckQuery query, CancellationToken ct = default);
}
```

`HouseholdData`, `CardDetails`, `AddressUpdateResult`, `CardReplacementResult`, `EnrollmentCheckResult` are existing (or lightly-extended) Core models. `StateCapabilities` and `HouseholdLookupQuery` are new.

### The generalized lookup

One input shape replaces the `GetHouseholdByX` method sprawl:

```csharp
public record HouseholdLookupQuery
{
    public required IReadOnlyList<IdentitySignal> Signals { get; init; }

    /// <summary>Primary = guardian's own household. CoLoad = find a SNAP/TANF household to load onto.</summary>
    public required LookupIntent Intent { get; init; }

    public bool IncludeCardDetails { get; init; } = false;
}

public enum LookupIntent { Primary, CoLoad }

/// <summary>One identity signal. Backends ignore types they don't recognize.</summary>
public record IdentitySignal(string Type, string Value, bool Verified = false);

/// <summary>Well-known signal types. Open string constants — new types need no portal change.</summary>
public static class IdentitySignalType
{
    public const string Email            = "email";
    public const string PhoneNumber      = "phone_number";
    public const string DateOfBirth      = "date_of_birth";
    public const string StateBenefitId   = "state_benefit_id";
    public const string FederalBenefitId = "federal_benefit_id";
    public const string StateIssuedId    = "state_issued_id";
}
```

### Capabilities

Mirrors the REST `GET /capabilities` shape as a Core model the evaluator consumes:

```csharp
public record StateCapabilities
{
    public required string SpecVersion { get; init; }
    public ServiceMode? ServiceMode { get; init; }

    public bool CoLoadedLookup { get; init; }
    public CardDetailsCapability? CardDetails { get; init; }
    public bool CardReplacement { get; init; }
    public bool CardReplacementStatusTracking { get; init; }
    public bool AddressUpdate { get; init; }
    public bool EnrollmentCheck { get; init; }
    public bool UserAssertion { get; init; }
}

public record CardDetailsCapability
{
    public bool Supported { get; init; }
    /// <summary>Well-known: "batch", "perCase".</summary>
    public IReadOnlyList<string> Modes { get; init; } = [];
}
```

## Applications

`LookupHouseholdAsync` returns applications alongside cases, because the REST `POST /households/lookup` response now carries both. Applications are read-only — the portal never updates them.

No new interface method is needed. Core `HouseholdData` already has `.Applications` (and `SummerEbtCase.ApplicationId` for the case→application link). So:

- `RestStateBackendClient` maps the response's `applications[]` into `HouseholdData.Applications`.
- `PluginAdapter` maps the plugin's applications the same way (DC/CO plugins already surface application data on `HouseholdData`).
- The use-case layer reads `HouseholdData.Applications` as it does today.

The one spec change that mattered here: `Application` dropped its benefit issue/expiration dates (those belong on the case) and gained `submittedDate` and `decisionDate`. The Core `Application` model should follow when we touch it, but that's out of scope for this spike.

## Dual-mode consumption

At startup, DI registration (in `Dependencies.cs` or a dedicated `StateBackendServiceCollectionExtensions`) binds exactly one `IStateBackendClient` from config:

```
StateBackend:BaseUrl configured?
  ├─ Yes → register RestStateBackendClient
  └─ No  → register PluginAdapter (wraps the connector loaded by ServiceCollectionPluginExtensions)
```

The use-case layer only ever sees `IStateBackendClient`. The REST-vs-plugin choice is resolved once at startup and is invisible above the infrastructure layer. One binding, chosen by config — no async work during registration, no capability-based conditional registration.

**`RestStateBackendClient`** is thin. HTTP call to `StateBackend:BaseUrl`, response mapped to Core models. Owns the resilience pipeline (retry, circuit breaker, timeout), the auth strategy (`oauth2` or API key), and — when `capabilities.userAssertion` is true — attaching the `X-Sebt-User-Identity` JWT.

**`PluginAdapter`** delegates to the current plugin interfaces (`ISummerEbtCaseService`, `ICardReplacementService`, `IAddressUpdateService`, `IEnrollmentCheckService`, `IStateHealthCheckService`). Its job is mechanical mapping only:

- `LookupHouseholdAsync(query)` dispatches on `query.Intent` and the signals present: `Primary` → the identifier-based lookup, picking the identifier from the signal list by type; `CoLoad` → the co-load lookup, reading `state_benefit_id` (or `federal_benefit_id`) + `date_of_birth` from the signals.
- Plugins already return `HouseholdData`, so there's effectively no output mapping.
- Writes delegate 1:1. The idempotency key rides as a correlation header; current DC/CO plugins don't enforce dedup, so it's advisory until they do.

## The one plugin reshape

**Extract co-loading out of `ISummerEbtCaseService` into its own optional service.** This is the single change we make to a plugin contract.

```csharp
// New optional plugin service. States that don't co-load simply don't export it.
public interface ICoLoadCaseService : IStatePlugin
{
    Task<bool> TryMatchAsync(string benefitId, DateOnly guardianDob, Guid portalUserId, CancellationToken ct = default);
    Task<HouseholdData?> GetHouseholdAsync(string benefitId, DateOnly guardianDob, string guardianLoginEmail,
        PiiVisibility piiVisibility, IdentityAssuranceLevel ial, Guid portalUserId, CancellationToken ct = default);
}
```

Why touch a contract we mean to retire:

- **It makes co-load capability derivable like everything else.** Today co-load is "every plugin implements the method, most return false" — the exact conflation of *method exists* with *capability supported* that blocks derivation. As a separate optional export, a real implementation's presence *is* the capability, and the adapter drops its one piece of state-specific knowledge.
- **It aligns the plugin model with the capability model**, so a later REST migration is a straight lift rather than an untangling.

Cost: a DC connector change (move two methods, keep behavior) and an interface-package version bump. CO is unaffected — it never implemented co-load. Reshaping a contract mid-life is a real cost, but the scope is two methods and it removes the adapter's only non-mechanical branch — worth it for a layer that may live for years.

## Capabilities: derived, not asserted

Neither DC nor CO exposes a capabilities endpoint, so `PluginAdapter.GetCapabilitiesAsync()` synthesizes `StateCapabilities` from what the connector actually registered — not from a hand-maintained map.

`ServiceCollectionPluginExtensions` backfills any service the connector didn't provide with a `DefaultXService`. So a capability is **true when the resolved implementation is not the `Default` type** — i.e. the connector really provided it. `AddressUpdate` ← real `IAddressUpdateService`; `CardReplacement` ← real `ICardReplacementService`; `EnrollmentCheck` ← real `IEnrollmentCheckService`; `CoLoadedLookup` ← real `ICoLoadCaseService` once the reshape lands.

Derived capabilities can't drift, because they *are* the wiring. For `RestStateBackendClient` capabilities come from a real HTTP call, cached per the spec's `Cache-Control: max-age` (default 5-minute TTL). For `PluginAdapter` they're computed from the DI container and effectively static.

`ISelfServiceEvaluator` gates `allowedActions` on capability support. That logic is unchanged — it reads `StateCapabilities` regardless of which client produced them.

### `X-Sebt-User-Identity` JWT

When `capabilities.userAssertion` is true, `RestStateBackendClient` generates a short-lived signed JWT per authenticated request. `PluginAdapter` never sends it — plugins run in-process and don't consume it.

```csharp
public interface IUserAssertionJwtService
{
    string GenerateToken(UserAssertion assertion);
}

public record UserAssertion
{
    public required IdentityAssuranceLevel Ial { get; init; }
    /// <summary>HMAC-SHA256 of the portal's internal user ID. Opaque, non-reversible.</summary>
    public required string UserRef { get; init; }
}

/// <summary>
/// IAL levels. The converter is the single source of truth for the wire mapping:
/// Ial1 -> 1, Ial1Plus -> 1.5, Ial2 -> 2. Enum everywhere in code; number on the wire.
/// </summary>
[JsonConverter(typeof(IdentityAssuranceLevelConverter))]
public enum IdentityAssuranceLevel { Ial1, Ial1Plus, Ial2 }
```

We use an enum, not a `decimal` — the levels are stable and a `decimal` field would admit nonsense values (e.g. `1.3`). The non-integer wire value (`1.5`) lives only in the `JsonConverter`, which maps enum↔number in one place. If a future level appears, it's a one-line enum + converter change.

`HS256` (shared secret) by default; `RS256` (portal signs, backend verifies with the portal's public key) when configured. Key material comes from config or a Docker secret path. 60-second TTL.

## Backwards compatibility

| State | Path | Connector change |
|---|---|---|
| DC | `BaseUrl` absent → `PluginAdapter` | Only the co-load reshape (two methods moved) |
| CO | `BaseUrl` absent → `PluginAdapter` | None |
| New states | `BaseUrl` present → `RestStateBackendClient` | N/A — implement the REST contract |

Adding `StateBackend:BaseUrl` to a state's config is the migration switch: it flips that state from plugin to REST with no other portal change.

## How far we take REST

The sequence, with the caveat that steps 3–4 are aspirations, not commitments:

1. **Now (this spike):** land the interface, `RestStateBackendClient`, `PluginAdapter`. DC and CO stay on plugins. Nothing user-facing changes.
2. **Next (follow-on POC, DC-569):** build a REST middleware in front of CO's existing API; point the portal at it via `BaseUrl`. First real exercise of `RestStateBackendClient` and proof the contract is implementable against a live state system.
3. **Maybe:** the same for DC — lower priority, and possibly never, given DC's constraints.
4. **Eventually, if we get there:** retire `PluginAdapter` and the connector package. Until then, the adapter is a supported, maintained path — not deprecated code.

## Open questions

| # | Question | Impact |
|---|---|---|
| OQ-1 | **Do all plugins populate the newly-added case fields?** The spec now carries `eligibilityType`, `eligibilitySource`, `isCoLoaded`, `isStreamlineCertified`, benefit dates, per-case `mailingAddress`, `displayNumber`, and card `balance`. DC and CO plugins may not surface all of them; the adapter maps what's present and omits the rest. Confirm no use-case treats an omitted field as an error. | Medium — silent display gaps if a handler assumes presence |
| OQ-2 | **`Application` Core-model cleanup.** The spec's `Application` dropped benefit issue/expiration dates and gained `submittedDate`/`decisionDate`. The Core `Application` model still carries the old fields. Aligning it is out of scope here but should be tracked — it overlaps the known `Cases vs Applications` tech debt. | Low — Core model lags the contract until addressed |
| OQ-3 | **IAL wire format: number vs. decimal string.** The converter emits a JSON number (`1.5`) by default. Some JWT libraries are fussy about non-integer numeric claims; if so, emit a decimal string (`"1.5"`) instead. Decide during implementation against the chosen JWT library. | Low — isolated to the converter |

## References

- [`docs/openapi.yaml`](../openapi.yaml) — REST contract; interface operations mirror it
- [`docs/adr/0017-generalize-state-backend-interface-contract.md`](../adr/0017-generalize-state-backend-interface-contract.md) — the architectural decision
- [DC-568](https://codeforamerica.atlassian.net/browse/DC-568) — this spike
- Current plugin loading: `src/SEBT.Portal.Api/Composition/ServiceCollectionPluginExtensions.cs`
- Current lookup contract: `sebt-self-service-portal-state-connector` → `ISummerEbtCaseService`
- Capability gate: `src/SEBT.Portal.Core/Services/ISelfServiceEvaluator.cs`
