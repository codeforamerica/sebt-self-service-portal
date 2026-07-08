# State Interface Generalization — Portal-Side Consumption Design

**Status:** Draft — spike in progress (DC-568)

---

## Problem

The portal's plugin interfaces have accreted state-specific detail. `ISummerEbtCaseService` carries co-loading methods (`TryMatchCoLoadedGuardianByBenefitIdAndDobAsync`, `GetHouseholdByBenefitIdentifierAndDobAsync`) documented "DC only; other states return false." The plugin system as a whole still assumes CFA-authored C# DLLs as the integration unit.

The broader REST contract redesign ([`docs/openapi.yaml`](../openapi.yaml)) defines what state backends expose. This TDD covers the _portal side_ of that split: the C# interfaces the portal's use-case layer calls, the dual-mode consumption strategy (REST vs. plugin), and the adapter seam that lets DC and CO keep their existing DLLs while new states take the REST path.

Two risks to name up front:

- **Adapter fidelity risk.** `PluginAdapter` is only as good as the existing plugin interfaces allow. Capabilities the plugins don't expose (e.g., co-loading in states that don't support it) will need explicit "not supported" returns. Getting this wrong produces silent feature gaps rather than loud failures.
- **Interface proliferation.** Splitting too aggressively into sub-interfaces makes DI registration and capability-gating complex. The shapes below are a reasonable first cut; the open questions section flags the biggest unresolved split.

---

## Goal

- Define generalized C# interfaces for portal-to-state-backend interaction that aren't tied to plugins or specific states.
- Describe the dual-mode strategy: REST client when `StateBackend:BaseUrl` is configured; plugin + adapter when it isn't.
- Preserve full backward compatibility for DC and CO without changing their connector DLLs.

---

## Proposed Interface Shapes

These are sketches — enough fidelity to communicate intent. Final signatures live in the `sebt-self-service-portal-state-connector` repo.

### Top-level interface

```csharp
/// <summary>
/// Unified entry point for portal-to-state-backend interaction.
/// Both the REST client and the MEF plugin adapter implement this interface.
/// </summary>
public interface IStateBackendClient
{
    Task<CapabilitiesResponse> GetCapabilitiesAsync(CancellationToken ct = default);
    Task<HealthResponse>       GetHealthAsync(CancellationToken ct = default);

    ICasesClient     Cases     { get; }
    IEnrollmentClient Enrollment { get; }
}
```

`GetCapabilitiesAsync()` is called on startup (and on ETag mismatch). The portal gates every operation on the result before delegating to `Cases` or `Enrollment`.

### Cases

```csharp
public interface ICasesClient
{
    Task<CasesLookupResponse>          LookupCasesAsync(CasesLookupRequest request, CancellationToken ct = default);
    Task<CardDetailsResponse>          GetCardDetailsAsync(string caseId, CancellationToken ct = default);
    Task<AddressUpdateBatchResponse>   UpdateAddressAsync(AddressUpdateRequest request, CancellationToken ct = default);
    Task<CardReplacementResponse>      RequestCardReplacementAsync(string caseId, CardReplacementRequest request, string idempotencyKey, CancellationToken ct = default);
    Task<CardReplacementStatusResponse> GetCardReplacementStatusAsync(string caseId, string requestId, CancellationToken ct = default);
}
```

### Enrollment

```csharp
public interface IEnrollmentClient
{
    Task<EnrollmentCheckResponse> CheckEnrollmentAsync(EnrollmentCheckRequest request, CancellationToken ct = default);
}
```

### Key request/response types

These sketch the shapes; property-level constraints live in the OpenAPI spec.

```csharp
/// <summary>
/// Request body for POST /cases/lookup.
/// </summary>
public record CasesLookupRequest
{
    public required IReadOnlyList<IdentitySignal> Signals { get; init; }

    /// <summary>
    /// Open string constant. Well-known values: "primary", "coLoad".
    /// Backends return 400 for unrecognized values.
    /// </summary>
    public required string Intent { get; init; }

    public bool IncludeCardDetails { get; init; } = false;
}

/// <summary>
/// A single identity signal passed to a state backend lookup.
/// Backends ignore signal types they don't recognize.
/// </summary>
public record IdentitySignal(string Type, string Value, bool Verified = false);

/// <summary>
/// Well-known signal type identifiers. Open string constants — new types can be
/// added without a portal code change; backends ignore unknown types.
/// </summary>
public static class IdentitySignalType
{
    public const string Email            = "email";
    public const string PhoneNumber      = "phone_number";
    public const string DateOfBirth      = "date_of_birth";
    public const string StateBenefitId   = "state_benefit_id";
    public const string FederalBenefitId = "federal_benefit_id";
    public const string StateIssuedId    = "state_issued_id";
}

/// <summary>
/// Well-known intent constants for CasesLookupRequest.Intent.
/// </summary>
public static class LookupIntent
{
    public const string Primary = "primary";
    public const string CoLoad  = "coLoad";
}

/// <summary>
/// State backend's capabilities response. The portal caches this and gates
/// every operation on the result.
/// </summary>
public record CapabilitiesResponse
{
    public required string SpecVersion  { get; init; }
    public ServiceMode?    ServiceMode  { get; init; }
    public required Capabilities Capabilities { get; init; }
}

public record Capabilities
{
    public CasesCapabilities?      Cases        { get; init; }
    public EnrollmentCapabilities? Enrollment   { get; init; }
    public UserAssertionCapabilities? UserAssertion { get; init; }
}

public record CasesCapabilities
{
    public CapabilityFlag?             CoLoadedLookup  { get; init; }
    public CardDetailsCapability?      CardDetails     { get; init; }
    public CapabilityFlag?             CardReplacement { get; init; }
    public CapabilityFlag?             AddressUpdate   { get; init; }
}

public record CardDetailsCapability
{
    public bool Supported { get; init; }

    /// <summary>
    /// Well-known values: "batch", "perCase". Extensible.
    /// </summary>
    public IReadOnlyList<string> Modes { get; init; } = [];
}

public record CapabilityFlag(bool Supported);

/// <summary>
/// The portal attaches this as a signed JWT in X-Sebt-User-Identity
/// on authenticated requests when userAssertion.supported is true.
/// </summary>
public record UserAssertion
{
    /// <summary>
    /// Identity assurance level: 1, 1.5, or 2.
    /// </summary>
    public required decimal Ial { get; init; }

    /// <summary>
    /// HMAC-SHA256 of the portal's internal user ID. Opaque and non-reversible.
    /// </summary>
    public required string UserRef { get; init; }
}
```

---

## Dual-Mode Consumption Strategy

At startup, the DI registration logic in `Dependencies.cs` (or a dedicated `StateBackendServiceCollectionExtensions`) checks config:

```
StateBackend:BaseUrl present?
  ├─ Yes  →  register RestStateBackendClient as IStateBackendClient
  └─ No   →  scan plugins-{state}/ for MEF DLLs
               ├─ Found  →  load plugin, wrap in MefPluginAdapter, register as IStateBackendClient
               └─ Not found  →  throw at startup (fail loud; don't silently degrade)
```

The portal's use-case layer only ever calls `IStateBackendClient`. The choice between REST and plugin is resolved once at startup and is invisible above the infrastructure layer.

### `RestStateBackendClient`

Implements `IStateBackendClient` directly. Makes HTTP calls to the configured `StateBackend:BaseUrl`. Applies the resilience pipeline (retry, circuit breaker, timeout) and `IStateBackendAuthStrategy`. Handles capability-based card detail loading strategy (`batch` vs. `perCase`).

### `MefPluginAdapter`

The backward-compat seam. Implements `IStateBackendClient` by delegating to the existing plugin interfaces (`ISummerEbtCaseService`, `ICardReplacementService`, `IAddressUpdateService`, `IEnrollmentCheckService`). This is where impedance mismatch between the old plugin shapes and the new interface shapes is absorbed.

Key adapter responsibilities:

- **`GetCapabilitiesAsync()`** — synthesizes a `CapabilitiesResponse` from the plugin's known capabilities. DC and CO plugins don't expose a capabilities endpoint; the adapter constructs a static response based on what we know each plugin supports. This is a config-driven or hardcoded mapping per state — acceptable for two states, not a pattern to scale.
- **`LookupCasesAsync()`** — maps `CasesLookupRequest` to the appropriate plugin method. `intent = "primary"` → `ISummerEbtCaseService.GetHouseholdByIdentifierAsync()`. `intent = "coLoad"` → `ISummerEbtCaseService.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync()` / `GetHouseholdByBenefitIdentifierAndDobAsync()` for states that support it; otherwise returns an empty result (capability-gating should have prevented the call).
- **`GetCardDetailsAsync()`** — delegates to the plugin's card lookup path. Plugin-side card details fetch is already per-case for DC and batch-optional for CO; the adapter maps to `perCase` or `batch` accordingly in the synthesized capabilities.
- Write operations (`UpdateAddressAsync`, `RequestCardReplacementAsync`) — delegate to existing plugin interfaces 1:1. The idempotency key is passed through as a request correlation header; existing DC/CO plugins don't enforce idempotency, so this is advisory.

### Capabilities-first gate

The portal calls `GetCapabilitiesAsync()` before any operation. For `RestStateBackendClient`, this is a real HTTP call (cached per TTL). For `MefPluginAdapter`, this returns the synthesized static response.

The `ISelfServiceEvaluator` already gates `allowedActions` on capability support. That logic is unchanged — it works against `CapabilitiesResponse` regardless of which adapter produced it.

### `X-Sebt-User-Identity` JWT generation

When `capabilities.userAssertion.supported` is `true`, the portal generates a signed JWT before each authenticated request. `RestStateBackendClient` attaches it as the `X-Sebt-User-Identity` header. `MefPluginAdapter` ignores this (existing plugins don't consume it).

JWT generation lives in a new `IUserAssertionJwtService`:

```csharp
public interface IUserAssertionJwtService
{
    string GenerateToken(UserAssertion assertion);
}
```

Algorithm is `HS256` by default; `RS256` if `StateBackend:UserAssertionSigningKeyType` is `"RSA"`. Key comes from `StateBackend:UserAssertionSigningKey` (config value or Docker secret path). 60-second TTL.

---

## Backwards Compatibility

| State      | Path                                                            | Plugin changes required |
| ---------- | --------------------------------------------------------------- | ----------------------- |
| DC         | `StateBackend:BaseUrl` absent → MEF plugin → `MefPluginAdapter` | None                    |
| CO         | `StateBackend:BaseUrl` absent → MEF plugin → `MefPluginAdapter` | None                    |
| New states | `StateBackend:BaseUrl` present → `RestStateBackendClient`       | N/A                     |

Migration path for DC and CO is out of scope for this spike. The adapter is the exit ramp — when either state is ready to migrate, adding `StateBackend:BaseUrl` to their config switches them to the REST path with no other code changes.

---

## Open Questions

| #    | Question                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      | Impact                                                                         |
| ---- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------ |
| OQ-1 | **How does `MefPluginAdapter.GetCapabilitiesAsync()` handle capabilities the existing plugins can't declare?** DC and CO plugins have no capabilities endpoint. The adapter must synthesize a response. That response is currently hardcoded per state. As capabilities evolve (e.g., new write operations), the hardcoded map drifts. Is a config-driven capabilities override (per-state `capabilities.json` alongside the plugin DLL) a better long-term answer?                                                                                                                                                                           | Medium — affects how reliably `allowedActions` reflects actual plugin behavior |
| OQ-2 | **Single `IStateBackendClient` vs. split capability-specific interfaces assembled at runtime?** The current proposal is a single interface with sub-clients. An alternative: the DI container registers only the interfaces the backend's `GetCapabilitiesAsync()` says it supports, and use-case code asks for `IEnrollmentClient` directly. This is more type-safe but complicates the startup registration flow (async DI bootstrap) and makes it harder to swap adapters in tests. Recommendation leans toward the single-interface approach with explicit capability-gating in callers, but this needs a decision before implementation. | High — shapes the entire DI registration and test strategy                     |
| OQ-3 | **DI registration pattern for the dual-mode setup.** A factory function (`Func<IStateBackendClient>`) resolved after config reads cleanly but delays failure discovery. A startup extension method that inspects `IConfiguration` directly and registers the concrete type is more explicit and fails at startup. The latter is preferred — but it needs a design that doesn't make the registration untestable.                                                                                                                                                                                                                              | Medium — affects startup reliability and test harness design                   |
| OQ-4 | **CO spike scope.** The CO proof-of-concept (name TBD — see DC-569) is the validation vehicle for these assumptions. That spike is a follow-on and is not in scope here. The interface shapes in this TDD are provisional until the CO spike validates them against a real integration.                                                                                                                                                                                                                                                                                                                                                       | High — these shapes may change                                                 |
| OQ-5 | **`ial` precision in `UserAssertion`.** The spec uses `decimal` for `ial` (values: 1, 1.5, 2). Is `decimal` the right C# type, or should this be an enum or a strongly-typed value object? `decimal` is flexible but allows nonsense values. An enum is safer but requires a version bump to add new levels. TBD pending alignment with the OpenAPI spec's approach.                                                                                                                                                                                                                                                                          | Low                                                                            |

---

## References

- [docs/openapi.yaml](../openapi.yaml) — canonical REST contract; C# interfaces derive from it

- [DC-568](https://codeforamerica.atlassian.net/browse/DC-568) — spike ticket
- [docs/adr/](../adr/) — ADR for the MEF plugin approach: `0007-multi-state-plugin-approach.md`; generalizing the interface pattern may produce a companion ADR (path TBD — ADR agent is drafting concurrently)
