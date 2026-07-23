# DC-568 Prototype: JSON-everywhere state integration

Prototype plan. Audience: engineers on this repo. Nothing here is settled; it's the plan
for a spike that stress-tests one thesis.

## Thesis

If every state backend speaks JSON over HTTP, per-state variation collapses to *differences
in JSON API shape + auth scheme*, and a single config-driven `IStateBackend` absorbs them
with **no per-state transport code**. We test it by wrapping DC's stored procs in a thin
REST service (so DC stops being a SQL special-case) and driving both DC and CO through one
config-parameterized implementation.

This is deliberately *not* the spike/state-api approach of making every state implement a
canonical contract. Each state exposes its own native JSON shape; the portal adapts via config.

## Repos & branches

| Repo | Branch | Holds |
|---|---|---|
| `sebt-self-service-portal` (monorepo) | `spike/dc-568-state-backend-json-adapter` | `IStateBackend` port + config-driven JSON/HTTP driver; per-state config bundles; this doc |
| `sebt-self-service-portal-dc-connector` (private) | same name | the thin DC REST wrapper |

Wrapper lives in the DC repo (kept private) so DC's sproc shapes stay out of public view,
and it reuses the existing mock-DB compose. Work happens in the main checkouts of both repos.

## Three pieces

### Piece 1 — DC REST wrapper `[Claude]`

Thin ASP.NET Core minimal-API service in the DC repo (new project
`src/SEBT.Portal.StatePlugins.DC.RestApi`, aligned with the repo's `.SocureApi` sibling).
**Not** a plugin; no runtime dependency on the MEF plugin — the plugin's mapping
(`MapToHouseholdData`, `InferIssuanceType`, disaggregation) is exactly what we're pulling
out into portal config. Port only the raw sproc mechanics as a *reference*.

- **Exact passthrough:** each endpoint calls one sproc, serializes raw result-set columns →
  JSON as-is (original names; multi-result-sets as arrays). Zero canonical mapping.
- **Auth:** API-key middleware (`X-Api-Key`), key from config/secret. `/health` open.
  Deliberately *different* from CBMS's OAuth2 client-credentials, to exercise varying auth
  in the config layer. (mTLS is the later prod-hardening upgrade.)
- **Surface & local-test reality:**

  | Endpoint | Sproc | Mock-DB backed today? |
  |---|---|---|
  | `POST /households/lookup` | `GetHouseholdByGuardian` (102) | ✅ |
  | `POST /address-updates` | `UpdateMailingAddress` (103) | ✅ |
  | `POST /card-replacements` | `RequestNewCard` | ❌ `NOT_CONFIGURED`; needs a mock proc in `scripts/sql/` |
  | `POST /enrollment/check` | enrollment proc | ❌ not seeded; needs a mock proc |
  | `GET /health` | `SELECT 1` | ✅ |

  Build the full surface; add mock sprocs (`scripts/sql/1xx_*.sql`, alphabetical load) to
  make card-replacement + enrollment integration-testable.
- **Compose:** add the wrapper as a service in the existing `docker-compose.yaml`,
  `depends_on: mssql (healthy)`, `DCConnector:ConnectionString` → `DcSource`.
- Non-root multi-stage Dockerfile per portal container standards.

### Piece 2 — `IStateBackend` abstraction `[you]`

The port UseCases depend on, in an isolated integration module, coexisting with the MEF
plugins. Strawman signature: see `#strawman` below. Cohesive façade + capability manifest;
split by ISP if preferred. `HouseholdData`/`SummerEbtCase`/`CardReplacementResult`/
`AddressUpdateResult` already exist in Core — reuse.

### Piece 3 — config-driven JSON/HTTP driver `[you]`

One implementation of `IStateBackend`, parameterized entirely by config. Primitives:
- **Auth:** per-state scheme — `client_credentials` (CO) vs `api_key` (DC). Proves varying auth.
- **Capability manifest:** static per-state config (startup, no negotiation call).
- **Structural field map:** canonical field ← source JSON path.
- **Enum-token maps:** value → canonical enum + default.
- **Disaggregation strategy:** capped predicate vocabulary (`presence` | `valueInSet`, named
  `caseInclusion` predicates). NOT an arbitrary-boolean DSL.
- **`strategy: bespoke` escape hatch:** explicit marker where (c) code still owns logic
  (e.g. DC's substring issuance inference) — don't fake it as config.

Config approach: **YAML for mapping/capability rulesets** (comments, anchors, readable
nested maps), **JSON/env + secrets for operational values**. Parse YAML with **YamlDotNet**
(MIT); optionally **NetEscapades.Configuration.Yaml** to bind via `IOptions`. Validate against
a schema at startup; fail loud.

## Sequencing (vertical slice first)

1. `[Claude]` Wrapper skeleton + `POST /households/lookup` + API-key auth, wired into compose. Testable lookup.
2. `[you]` `IStateBackend` port + JSON/HTTP driver + DC config bundle → portal reads a household through the wrapper, mapped by config.
3. **Checkpoint:** DC lookup end-to-end (portal → wrapper → sproc → raw JSON → config map → canonical), MEF plugin untouched alongside.
4. `[you]` Add CO/CBMS config bundle → same driver, second state, only config differs. **Moment of truth for the config bet.**
5. `[Claude]` Fill out wrapper surface (address update against mock; card-replacement + enrollment after mock sprocs added).
6. `[you]` Extend primitives to writes (result normalization, idempotency-key wiring). Cooldown stays portal-side.

## Untouched

Portal operational DB, cooldown, HMAC hashing, existing MEF plugins — all as-is, coexisting.
Parallel path proven on DC first, not a rip-and-replace. The `IStateBackend` seam keeps the
option to extract an out-of-process service per state later (dual-mode), but that's deferred.

## Strawman interface {#strawman}

```csharp
namespace SEBT.Portal.Integration;

public interface IStateBackend
{
    StateBackendCapabilities Capabilities { get; }                         // config-loaded, no negotiation

    Task<HouseholdLookupResult> LookupHouseholdAsync(
        HouseholdLookupRequest request, CancellationToken cancellationToken = default);

    Task<CardDetails?> GetCardDetailsAsync(                                 // only if Capabilities.CardDetails.Modes has PerCase
        string caseId, CancellationToken cancellationToken = default);

    Task<CardReplacementResult> RequestCardReplacementAsync(               // idempotency key required; cooldown stays portal-side
        CardReplacementRequest request, CancellationToken cancellationToken = default);

    Task<AddressUpdateResult> UpdateAddressAsync(                          // may report per-case failures when non-atomic
        AddressUpdateRequest request, CancellationToken cancellationToken = default);

    Task<EnrollmentCheckResult> CheckEnrollmentAsync(
        EnrollmentCheckRequest request, CancellationToken cancellationToken = default);

    Task<StateBackendHealth> GetHealthAsync(CancellationToken cancellationToken = default);
}

public sealed record HouseholdLookupRequest(IReadOnlyList<IdentitySignal> Signals);
public sealed record IdentitySignal(string Type, string Value, bool Verified);
public sealed record HouseholdLookupResult(HouseholdLookupStatus Status, HouseholdData? Household);
public enum HouseholdLookupStatus { Found, NotFound, Ambiguous }

public sealed record StateBackendCapabilities(
    string SpecVersion,
    ServiceMode ServiceMode,               // Full | ReadOnly | Maintenance
    CardDetailsCapability CardDetails,     // Modes: [Batch|PerCase]
    CardReplacementCapability CardReplacement,
    bool AddressUpdate,
    bool EnrollmentCheck);
```

## Config samples (same schema, per-state values)

Structure: operations keyed by the closed `OperationType` enum (presence ⇒ capability, so the manifest is *derived*, not duplicated); mappings live under each operation; `enums` shared at state level (targets validated against the C# enum at startup, fail-loud); secrets are config-key references, never values.

```yaml
# state-backends/dc.yaml — DC via the thin REST wrapper
id: DC
baseUrl: ${DC_WRAPPER_BASEURL}

auth:
  scheme: api_key                          # ← differs from CO
  header: X-Api-Key
  keyRef: StateBackend:Auth:ApiKey         # config KEY, resolved from env / /run/secrets — never the value

identifiers:
  preferred: [email]

enums:                                     # shared state-level vocabulary; every target validated vs the C# enum at startup (fail-loud)
  cardStatus:   { default: Unknown, map: { PROCESSED: Processed } }
  issuanceType: { strategy: bespoke }      # explicit (c) escape hatch: DC substring inference stays code

operations:                                # keyed by closed OperationType enum; an operation's presence IS its capability
  householdLookup:
    endpoint: "POST /households/lookup"
    request:                               # canonical input → state param shape (capped binding vocab: from / compose / const)
      guardianEmail:       { from: signal.email }
      guardianIdentifiers: { compose: { IC: signal.ic, DOB: signal.dob } }
    response:
      root: "$.resultSets[0]"              # raw multi-result-set passthrough from the wrapper
      fields:
        caseId:    SummerEBTCaseID
        childName: ChildName
        issueDate: IssueDate
      disaggregation:
        rule: presence                     # {presence | valueInSet}
        discriminatorField: ApplicationId
        groupApplicationsBy: ApplicationId
        caseInclusion: all                 # named predicate — NOT an expression DSL
  cardReplacement:
    statusTracking: false                  # per-op capability flag → folded into the derived manifest
    endpoint: "POST /card-replacements"
    request:
      caseId:         { from: caseId }
      idempotencyKey: { from: idempotencyKey }
```

```yaml
# state-backends/co.yaml — CO via CBMS (existing JSON REST)
id: CO
baseUrl: ${CBMS_BASEURL}

auth:
  scheme: client_credentials               # ← differs from DC
  tokenUrl: ${CBMS_TOKEN_URL}              # not secret — endpoint
  clientId: ${CBMS_CLIENT_ID}              # not secret — identifier
  clientSecretRef: StateBackend:Auth:ClientSecret   # config KEY only; value from env / /run/secrets / vault, resolved at token fetch
  scope: sebt

identifiers:
  preferred: [phone]

enums:
  caseStatus: { default: Unknown, map: { AP: Approved, DE: Denied } }
  cardStatus: { default: Unknown, map: { ACTIVE: Active, "LOST, AUTO REISSUE": Lost } }

operations:
  householdLookup:
    endpoint: "POST /sebt/get-account-details"
    request:
      phone: { from: signal.phone }
    response:
      root: "$.stdntEnrollDtls"
      fields:
        caseId:    sebtChldCwin
        childName: chldNm
      disaggregation:
        rule: valueInSet
        discriminatorField: eligSrc
        applicationValues: [CBMS, PK]      # these values mean "application-based"
        groupApplicationsBy: sebtAppId
        caseInclusion: whenApprovedOrNotApplicationBased   # named predicate from the capped vocabulary
  cardReplacement:
    statusTracking: false
    endpoint: "PATCH /sebt/update-std-dtls"   # CO reuses one endpoint for card replacement + address update…
    request:
      caseId:         { from: caseId }
      idempotencyKey: { from: idempotencyKey }
  addressUpdate:
    endpoint: "PATCH /sebt/update-std-dtls"   # …declared independently per operation — coincidental sharing, not factored out
    request:
      caseIds: { from: caseIds }
      address: { from: address }
```

## Open items

- Add mock sprocs for `RequestNewCard` + enrollment so writes are integration-testable.
- Field-path syntax: simple dotted/JSONPath (`$.a.b`) vs a small expression lib — start simple; revisit only if a real map needs more.
- Where `IStateBackend` lives (new `SEBT.Portal.Integration` assembly vs evolving `apps/connectors`).
