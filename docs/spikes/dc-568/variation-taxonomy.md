# Per-State Variation Taxonomy — SEBT Portal (CO vs DC)

> Analysis artifact for DC-568. Built by diffing the two existing state connectors
> (Colorado CBMS + DC) to ground the question: *how much of per-state integration
> reduces to shared config over reusable primitives, vs genuinely bespoke code?*
> Every behavioral claim is anchored to `file:line`. Nothing here is a decision —
> it's evidence for revising the spike.

Two backends, two integration shapes. **CO** = HTTP/REST to Colorado CBMS via a Kiota
client, OAuth2 client-credentials, HybridCache SWR. **DC** = raw ADO.NET stored-proc RPC
over a SQL connection string, no caching. Both are MEF plugins implementing the same
`IStatePlugin`-derived interfaces; the portal maps their output into one canonical
`HouseholdData` at the repository boundary and owns all cross-cutting state (cooldown,
hashing, self-service gating, ProblemDetails).

The single most important structural finding: **the current contract has no capability
negotiation.** `IStatePlugin` is a bare marker (`IStatePlugin.cs:3`) and `StateMetadata`
carries only `Name` (`Data/StateMetadata.cs:5`). A state "supports" a capability by
exporting the interface (MEF → non-null `GetExport<T>()`) or by returning `null`/`false`
from a method. The target `docs/openapi.yaml` replaces this with an explicit, versioned
`GET /capabilities` document (`serviceMode` + structured `capabilities` map). That shift
is the backbone of the platform bet — it turns implicit, code-encoded capability into
declared, config-shaped data.

## 1. Summary table

Classification key:
- **(a) Plumbing / shared-runtime** — identical or trivially parameterized across states.
- **(b) Config-over-a-shared-primitive** — differs per state but reduces to configuring a reusable primitive.
- **(c) Genuinely bespoke** — real code, not generalizable from 2 states; promote to (b) only after a 3rd state shows the same shape.

| # | Concern | CO behavior | DC behavior | Class | Shared primitive + config that parameterizes it |
|---|---|---|---|---|---|
| 1 | Household lookup / transport | HTTP `POST /sebt/get-account-details`, body `{PhnNm}`, `ebtCardService` query flag; Kiota client (`ColoradoSummerEbtCaseService.cs:102-151`, `PluginCache.cs:74-95`). **Phone only** — email path stubbed to null (`:64-73`) | Stored-proc RPC `GetHouseholdByGuardian` over `SqlConnection`, fixed 4-param shape, drain extra result sets (`DcSummerEbtCaseService.cs:68-80,184-214`). **Email only**; other types → null (`:147-150`) | **(c)** transport; **(b)** identifier acceptance | Transport is genuinely bespoke (REST client vs ADO.NET). *But* accepted-identifier set is already config: portal-side `StateHouseholdId:PreferredHouseholdIdTypes` ordered list (`StateHouseholdIdSettings.cs:17`) drives resolution (`HouseholdIdentifierResolver.cs:56-66`). Target contract generalizes transport to one REST `POST /households/lookup` with a `signals[]` array — collapses (c)→(a) |
| 2 | Case vs application disaggregation | Classifier keyed on CBMS `EligSrc`: `{CBMS,PK}`=application-based, `{DIRC,CDE}`=streamlined (`EligibilitySourceClassifier.cs:14-24`); cases = non-app-based OR (app-based AND approved); apps grouped by `SebtAppId` (`CbmsResponseMapper.cs:120-161`) | Split the one flat row set: case if `SummerEBTCaseID` present, application if `ApplicationId` present, grouped by `ApplicationId` (`DcSummerEbtCaseService.cs:431-479`); `IsStreamlineCertified = empty ApplicationId` (`:556`) | **(b)** ⚠️ | **Disaggregation strategy primitive.** Config names: (a) the source field (`EligSrc` vs presence-of-`ApplicationId`), (b) the value-set meaning "application-based", (c) the grouping key. CO adds an approval gate DC lacks — that's an extra config predicate. **Caveat: two states already need two different predicate shapes; cap the predicate vocabulary deliberately or this drifts toward a boolean-expression DSL.** |
| 3 | Enum/value mapping (card, app, issuance status) | Token→enum switches: card status full-word (`CbmsResponseMapper.cs:205-222`), case status 2-letter (`:167-182`), app status 2-letter (`:188-203`); unmapped→`Unknown`+log | App status upper-cased word switch (`DcSummerEbtCaseService.cs:582-593`); source col `ApplicationStatus` ∥ `EligibilityStatus` (`:397-398`) | **(b)** | **Enum-token map primitive.** State supplies a `value→canonical-enum` table (+ default). Textbook (b): pure data. Portal canonical enums (`CardStatus.cs`, `ApplicationStatus`, `IssuanceType`) are the fixed target vocabulary. Key normalization (uppercase) trivially parameterized |
| 4 | Derived/inferred fields | `IsStreamlineCertified = !isApplicationBased` (`CbmsResponseMapper.cs:110`); DOB month/day transposition retry in enrollment (`ColoradoEnrollmentCheckService.cs:237-246`); issuance **hard-coded** `SummerEbt` (`:99`) | `InferIssuanceType` substring inference over `HouseholdType`/`EligibilityType` (`OSSE`/`NSLP`→SummerEbt, `FOOD`/`SNAP`→Snap, `CASH`/`TANF`→Tanf, order-sensitive, `DcSummerEbtCaseService.cs:612-633`); card status = date-presence (`:550`) | **(c)** | Genuinely bespoke. Substring inference (DC), date-presence status (DC), hard-coded-single-value (CO) are three unrelated shapes. A "keyword→enum, ordered rules" primitive could subsume DC's `InferIssuanceType`, but CO doesn't exercise it. Do not generalize from 2 |
| 5 | Card details loading | Co-loaded in the single `get-account-details` call; `ebtCardService=Y/N` query flag gates inclusion; cache keys `:full`/`:shell` (`CbmsHouseholdCache.cs:46-47`) | Co-loaded in the single proc result set; `includeCardService` param **ignored**, card cols always read (`DcSummerEbtCaseService.cs:417-423`) | **(a)** | Both co-load in one round trip. Target contract makes this an advertised mode: `cardDetails.modes:[batch\|perCase]`. Both current states are `batch`; portal reads inline |
| 6 | Card replacement | Reuses `PATCH /sebt/update-std-dtls` with `ReqNewCard=Y` (`CbmsCardReplacementMapper.cs:23-36`); per-CaseRef match with `(sebtAppId,sebtChldId)` then fallback `sebtChldCwin` (`:261-276`); success=`respCd∈{200,00}`; structured `ErrorResponse` (`ColoradoCardReplacementService.cs:207-293`) | Per-case proc `RequestNewCard`, `ExecuteNonQuery`, OUTPUT `@resultCode`/`@resultMessage`; **fail-fast not atomic** (`DcCardReplacementService.cs:134-142`); policy-vs-error disambiguated by **substring `"policy"`** in free text (`:265-284`) | **(b)** result shape; **(c)** batch semantics + error signaling | Result shape already shared: `CardReplacementResult{IsSuccess,IsPolicyRejection,ErrorCode,ErrorMessage}`. **Idempotency is portal-owned** — not per-state. Bespoke: DC's free-text policy parsing vs CO's structured codes; DC fail-fast vs CO single-PATCH-atomic |
| 7 | Address update | Resolves write-ids for **all** actionable rows via `CbmsGetAccountStudentDetailIds.Resolve` heuristic (`:31-46`); one PATCH array, single `respCd`, **no partial success**; write-through to cache (`ColoradoAddressUpdateService.cs:146-206`) | Single proc, `@householdIdentifier`=email, structured `@resultCode` 0/1/2+ (`DcAddressUpdateService.cs:104-143`); no write-id resolution (email is the key); no partial success | **(b)** result; **(c)** write-id resolution | Result shape shared (`AddressUpdateResult`, 3-way). Bespoke: CO's heuristic id-resolution from Kiota `AdditionalData` (defends against non-camelCase CBMS keys) vs DC passing raw email |
| 8 | Auth to backend | OAuth2 client-credentials, HTTP Basic, in-memory token cache, 60s refresh buffer, `SemaphoreSlim` stampede guard (`ClientCredentialsTokenProvider.cs:58-112`) | SQL connection string (`DCConnector:ConnectionString`); no token (`DcSummerEbtCaseService.cs:23-24`) | **(a)** for HTTP states; **(c)** for SQL | For any HTTP backend, the OAuth2 client-creds provider is fully reusable plumbing — only URL+creds are config. DC's SQL-string auth has no HTTP analog. Target contract mandates OAuth2/API-key over HTTP → eliminates SQL transport → DC becomes an HTTP shim → collapses to (a) |
| 9 | Caching | HybridCache (L1+Redis), SWR soft=15m/hard=4h, negative-cache 60s by value, stampede coalescing, write-through, hashed-phone keys (`CbmsHouseholdCacheOptions.cs:5-8`) | **None.** Fresh SqlConnection every call | **(a)** | The SWR/negative-cache/stampede layer is generic runtime plumbing keyed on the lookup identifier. Nothing CO-specific except TTL values (`Cbms:Cache:*` config). Build once as a shared caching decorator; DC gets it free |
| 10 | Guardian/household matching | Read-only from first non-DD row (`CbmsResponseMapper.cs:44-51`); no active matching (match stubs return false/null) | Proc does the matching; C# builds `@guardianIdentifiers` JSON (`{PortalUUID,IC,DOB}`); proc ORs email and IC+DOB → can return multiple households (`DcSummerEbtCaseService.cs:107-116,326-327`) | **(c)** | Genuinely divergent. CO delegates all matching to phone lookup; DC has a real co-loaded IC+DOB match path (contract methods exist *for DC only* — `ISummerEbtCaseService.cs:46-56`). Clearest bespoke concern; contract even documents "other states should return false" |
| 11 | Identifier handling / correlation | Backend ids: `sebtChldCwin`→`SummerEBTCaseID`, per-year `sebtChldId`/`sebtAppId` in writes; robust `Resolve` over `AdditionalData`; enrollment correlated by echoed `StdReqInd` index | `PortalUUID` (portal `User.Id`) passed as correlation-only; proc mints its own log id; single case column → both `EbtCaseNumber` and `CaseDisplayNumber` (`DcSummerEbtCaseService.cs:82-90,546-548`) | **(a)** portal side; **(c)** connector side | **Portal-owned + shared:** normalization (`IdentifierNormalizer` strips dashes/spaces, `:39`) + HMAC-SHA256 hashing (`IdentifierHasher.Hash`) for cooldown/dedup, one `SecretKey`, not per-state. Connector-side id *semantics* (CWIN vs PortalUUID) are bespoke |
| 12 | Health check | Registered into `IHealthChecksBuilder`; `co-cbms-api-ping` authenticates + calls `GET /ping` (`CbmsApiHealthCheck.cs:17-33`); fallback `AlwaysDegradedHealthCheck` | Registered; `dc-sql-connectivity` runs `SELECT 1` (`DcSqlHealthCheck.cs:10-31`); same `AlwaysDegraded` fallback | **(a)** model; **(c)** probe body | **Model identical** (registered/push, unconfigured→AlwaysDegraded). Only the probe body differs (HTTP ping vs `SELECT 1`) — one line of state-specific code behind a shared registration pattern. Target contract flips to **pull** `GET /health` — a single shared poller |
| 13 | Error/result signaling | Structured `ErrorResponse` (Kiota) `errorDetails[]`; portal-facing codes (`INVALID_IDENTIFIER`, `HOUSEHOLD_NOT_FOUND`…); string `respCd∈{200,00}` success | **Mixed:** address = clean numeric `@resultCode`; card = **free-text substring `"policy"`** (`DcCardReplacementService.cs:265-284`); SqlException→structured codes | **(b)** result objects; **(c)** DC's free-text card path | Result objects normalize to shared 3-way + `ErrorCode`/`ErrorMessage`. Portal→HTTP mapping fully shared (`MvcResultExtensions.cs`: `DependencyFailed`→502, policy→409). Bespoke: DC free-text `"policy"` match — flagged in-code as temporary |

## 2. Config↔code boundary

**The line sits at the shape of the data, not the meaning of the values.** Anything that
reduces to "read field X, apply table/predicate Y, emit canonical value Z" is config over a
shared primitive (b): enum-token maps (#3), case/application disaggregation (#2),
self-service action gating (already fully config: `SelfServiceRulesSettings` keyed by
issuance-type × card-status), identifier-type preference (#1), cache TTLs (#9),
result-object construction (#6/#7/#13). Anything that is *runtime plumbing indifferent to
the state's semantics* is shared runtime (a): OAuth2 token lifecycle, SWR/negative-cache/
stampede, health registration, HMAC hashing/normalization, cooldown enforcement,
ProblemDetails mapping, idempotency. Bespoke (c) is reserved for logic that inspects
state-specific data *structure* in a way no table captures: DC's `InferIssuanceType`
substring rules (#4), DC's IC+DOB OR-matching (#10), CO's heuristic write-id extraction
from Kiota `AdditionalData` (#7/#11), DC's free-text policy parsing (#13).

**Promotion rule:** accept bespoke on first appearance; do not pre-generalize from two
states. Promote a (c) to a configured (b) primitive only when a **third** state exhibits
the *same shape* (not the same concern — the same shape). The trap to avoid is dressing up
a one-off as config-driven — DC's date-presence card status (#4) looks parameterizable but
is a genuine semantic choice, not a mapping table.

## 3. Effort split for a new state

Rough allocation of net-new integration work for state #3, under the **current** contract:

- **(a) plumbing, ~35%** — auth token lifecycle, caching, health registration, hashing,
  ProblemDetails, cooldown are built and shared. A new HTTP state inherits almost all of it;
  a new SQL state inherits less (no cache/OAuth reuse). Near-zero new code.
- **(b) config-over-primitive, ~25%** — enum-token maps, disaggregation discriminator/
  grouping, identifier-type preference, self-service rules, cache TTLs. Bounded and
  declarative *once the primitives exist* — but today only self-service gating and
  identifier-preference actually exist as config; enum maps and disaggregation are still
  hand-written per state.
- **(c) bespoke, ~40%** — transport client, matching logic, write-id resolution,
  derived-field inference, error-signal parsing. **Dominant cost, front-loaded on
  transport + matching.**

Biggest per-state costs, ranked: **(1) transport + matching (#1/#10)**; **(2) write-path
id resolution + error parsing (#6/#7/#13)**; **(3) enum maps (#3)** — high volume, low
difficulty.

**The target REST contract changes this split materially.** By mandating HTTP +
OAuth2/API-key + RFC 9457 + a `signals[]` lookup + opaque echoed `caseId`, it moves
transport (#1), auth (#8), caching (#9), health (#12), and error signaling (#13) firmly
into (a) *for the portal*. It converts the portal side of a new state from "write a
connector" to "point at a URL and supply config." The bespoke residue relocates onto the
**backend team's** side of the wire — which is where state-specific knowledge lives. That
relocation is the real payoff (and the real question: who owns/operates those adapters?).

## 4. Generalization stress test (hypothetical state #3)

1. **Per-case card fetch (N+1), not co-loaded batch.** Both current states co-load (#5). A
   state exposing cards only via a separate per-card endpoint needs `cardDetails.modes:[perCase]`
   + `GET /cases/{caseId}/card`. **(b) under the target contract** (capability pre-modeled);
   **(c) under the current interface** (no per-case card port). Strongest argument for
   adopting the capability model now.
2. **Multi-signal AND-matching with fuzzy names.** CO matches on phone; DC on email OR
   (IC+DOB). A state requiring `email + last_name + DOB` AND-matched with fuzzy tolerance
   fits neither. **(c) new code** under both. Matching semantics are irreducibly
   state-specific; the contract wisely pushes them across the wire. Promote to a "match
   strategy" primitive only if a 4th state shows the same shape.
3. **Asynchronous card replacement with status polling.** Both current states are
   synchronous. A state that queues + returns a `requestId` to poll fits neither.
   **(b) under the target contract** (`cardReplacement.statusTracking` + poll endpoint
   pre-modeled); **(c) under the current interface** (`CardReplacementResult` has no
   request-id/status field).

**Honest read:** two of three stress cases are **(b) only because the target contract
pre-modeled them** and **(c) under today's interface.** That asymmetry is the case for
adopting the capability model. The one case that stays (c) under both — multi-signal fuzzy
matching — is the honest limit: matching is irreducibly state-specific and should be pushed
to the backend, not pretended into portal config.

## Key file anchors

- Contract (marker, no capability model): `apps/connectors/state/src/SEBT.Portal.StatesPlugins.Interfaces/IStatePlugin.cs:3`, `Data/StateMetadata.cs:5`
- Canonical enums (fixed target vocabulary): `apps/portal/src/SEBT.Portal.Core/Models/Household/{CardStatus,IssuanceType,ApplicationStatus}.cs`
- Existing (b) primitives already config-driven: `SelfServiceRulesSettings.cs`, `StateHouseholdIdSettings.cs`, `CoLoadedCohortClassifier.cs`
- Portal-owned cross-cutting (a): `IdentifierHasher.cs`, `IdentifierNormalizer.cs:39`, `RequestCardReplacementCommandHandler.cs:40,158-162` (cooldown), `MvcResultExtensions.cs:78-79` (502 mapping)
- Bespoke (c) hotspots: `DcSummerEbtCaseService.cs:612-633` (InferIssuanceType), `DcCardReplacementService.cs:265-284` (free-text policy), `CbmsGetAccountStudentDetailIds.cs:31-46` (heuristic write-id), `DcSummerEbtCaseService.cs:107-116,326-327` (IC+DOB OR match)
- Target contract: `docs/openapi.yaml` (capabilities model ~940-1010, lookup ~180-330)
