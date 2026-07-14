# Plugin Adapter — Trial Planning (DC focus)

**Status:** Spike scratch — pressure-testing the `PluginAdapter` plan in `docs/tdd/state-interface-generalization.md` against DC's real connector. Not an implementation plan.

## Summary

- **The adapter is *not* mechanical the way the TDD claims.** Three interface operations have no DC plugin method behind them: `GetCardDetailsAsync(caseId)`, `GetCardReplacementStatusAsync`, and — structurally — `RequestCardReplacementAsync(caseId, …)`. Card details on DC arrive *inline* on the lookup, not per-case. The adapter must synthesize or fail these, and the TDD doesn't say which. **High.**
- **Card replacement is the worst mismatch.** The interface is per-`caseId`; DC's plugin takes `HouseholdIdentifierValue` + a list of `CaseRef` *triples* (`SummerEbtCaseId`, `ApplicationId`, `ApplicationStudentId`) and loops internally. The proposed signature drops the household identifier (which DC needs as `@householdEmail`) and the disambiguation triple. Not adaptable as written. **High.**
- **PII/IAL/portalUserId threading is a real gap, not "in-process so moot."** DC's read methods *require* `PiiVisibility`, `IdentityAssuranceLevel`, and `portalUserId` (the last flows to the warehouse as `PortalUUID` for correlation and is *load-bearing*, not cosmetic). The interface hides all three behind `X-Sebt-User-Identity`, which the TDD says `PluginAdapter` never sends. So the adapter needs an *ambient* context source the TDD hasn't specified. **High.**
- **Co-load reshape is genuinely small** — two methods move, behavior unchanged, CO untouched. This part of the TDD holds up. **Low.**
- **Capability derivation works for the write services** (real-vs-`Default`), **but can't derive `CardDetails` modes or `statusTracking`** because DC has no service to inspect for those. Those capabilities must be hardcoded/asserted for plugin states, contradicting "derived, not asserted." **Medium.**

## How mechanical is it really?

Per-operation walk against DC's actual connector.

### `LookupHouseholdAsync`, `intent=Primary`

Reasonably clean. DC's `GetHouseholdByIdentifierAsync` only supports `Email` (everything else returns null). So:

- Adapter reads the `email` signal from `query.Signals`, calls `GetHouseholdByGuardianEmailAsync` (or `GetHouseholdByIdentifierAsync` with `Email`).
- Output maps 1:1 — DC already returns `HouseholdData`. **True: no output mapping.**

Ambiguity: the TDD says "picking the identifier from the signal list by type." DC accepts *only* email. If the portal sends a `phone_number` or `state_benefit_id` signal for `Primary` (CO does phone), the adapter picks it, DC returns null, and the user sees "not found" rather than "unsupported." The adapter has no capability signal that says "DC only does email lookup," so it can't fail loudly. Minor, but it's a place where "mechanical" hides a silent-null.

### `LookupHouseholdAsync`, `intent=CoLoad`

Also clean *once the co-load reshape lands*. Adapter reads `state_benefit_id` (or `federal_benefit_id`) + `date_of_birth`, calls `ICoLoadCaseService.GetHouseholdAsync`. Maps 1:1.

But note the DC method needs **five** inputs the interface query doesn't obviously carry: `benefitId`, `guardianDob`, `guardianLoginEmail`, `piiVisibility`, `ial`, `portalUserId`. `guardianLoginEmail` is *required* (DC sets `HouseholdData.Email` from it because warehouse rows omit it) and isn't an identity signal — it's session context. See context threading below.

### `GetCardDetailsAsync(caseId)` — **no DC method exists**

DC has no card service. Card fields (`EbtCardLastFour`, `EbtCardStatus`, `EbtCardIssueDate`, `EbtCardBalance`) are populated inline on `SummerEbtCase` during the household lookup. The spec models this as `cardDetails.modes: [batch]` (inline on lookup) vs `perCase` (`GET /cases/{caseId}/card`). DC is `batch`-only.

So `PluginAdapter.GetCardDetailsAsync` has nothing to call. Options:
- Return a "not supported" sentinel / null and rely on `cardDetails.modes` never advertising `perCase` for DC.
- Re-run the lookup and extract the one case — but the adapter has no household identifier from a bare `caseId`, and DC can't look up by case ID.

The TDD's interface lists `GetCardDetailsAsync` as a first-class op but never says how `PluginAdapter` satisfies it for a batch-only state. **This is an unhandled operation, not a mechanical map.**

### `RequestCardReplacementAsync(caseId, request, idempotencyKey)` — **signature mismatch**

DC's `ICardReplacementService.RequestCardReplacementAsync(CardReplacementRequest)` takes:
- `HouseholdIdentifierValue` (→ SP `@householdEmail`, *required*),
- `IReadOnlyList<CaseRef>` — the `(SummerEbtCaseId, ApplicationId, ApplicationStudentId)` triple, explicitly there to disambiguate cases when rows share a per-child id,
- `Reason`.

The proposed interface is per-single-`caseId` and carries no household identifier and no triple. The real caller (`RequestCardReplacementCommandHandler`) passes the whole batch of `CaseRefs` and the resolved `identifier.Value`. Mapping the interface's `(caseId, request)` back onto DC's shape means the adapter must *recover* the household identifier and the application/student ids from somewhere — they aren't in the interface signature.

This isn't impedance the adapter can paper over. Either the interface's card-replacement signature changes to carry household identifier + case-ref triples (matching the REST body), or the handler keeps calling the plugin service directly and this op stays outside `IStateBackendClient`. **The TDD's one-line "writes delegate 1:1" is wrong for DC card replacement.**

### `GetCardReplacementStatusAsync` — **no DC method exists**

DC has no status polling. `DcCardReplacementService` is fire-and-forget against a stubbed SP. Adapter has nothing to call; capability must report `statusTracking.supported: false`. Fine, *if* the capability is right — but see derivation gap.

### `UpdateAddressAsync`

Clean. DC `IAddressUpdateService.UpdateAddressAsync(AddressUpdateRequest)` maps 1:1 to the interface. `AddressUpdateResult` is already the shared model. The interface's `AddressUpdateRequest` must carry `HouseholdIdentifierValue` (DC's SP keys on it); confirm the REST-derived request shape does.

### `CheckEnrollmentAsync`

Clean. DC `IEnrollmentCheckService.CheckEnrollmentAsync(EnrollmentCheckRequest)` → `EnrollmentCheckResult`, 1:1.

### `GetHealthAsync`

**Model mismatch.** DC's `IStateHealthCheckService.ConfigureHealthChecks(IHealthChecksBuilder)` is a *registration-time* hook — it adds an ASP.NET health check into the app's `HealthCheckService` at startup. It does not return a `StateHealth` on demand. The interface's `GetHealthAsync()` implies a pull. The adapter would have to resolve `HealthCheckService` and run the DC-tagged check, then map `HealthReport` → `StateHealth`. Doable, but it's an *inversion* of DC's model, not a delegation. The TDD lists `IStateHealthCheckService` as one of the delegated services without noting the shape flip.

### `GetCapabilitiesAsync`

Synthesized from DI — see derivation reality check.

## The co-load reshape, examined

This is the part of the TDD that holds up. Concrete DC impact:

**What moves:** `TryMatchCoLoadedGuardianByBenefitIdAndDobAsync` and `GetHouseholdByBenefitIdentifierAndDobAsync` leave `ISummerEbtCaseService`, land on a new `ICoLoadCaseService`. In `DcSummerEbtCaseService`, both are self-contained (they build their own JSON identifiers, open their own connection); no shared private state ties them to the email path beyond helper methods (`BuildGuardianIdentifiersJson`, `MapToRow`, `MapToHouseholdData`, `GetProcName`).

**What that costs DC:** either split `DcSummerEbtCaseService` into two classes (one per interface — the loader enforces *one* service interface per plugin type, see `ServiceCollectionPluginExtensions` line 78-89, so **a class can't export both `ISummerEbtCaseService` and `ICoLoadCaseService`**), or keep one class and let it export only `ICoLoadCaseService` for those methods. The mapping helpers (`MapToRow`, `MapToHouseholdData`, `InferIssuanceType`, `MapApplicationStatus`, `MapToCase`, `MapToApplication`, the `HouseholdMemberRow` DTO) are shared by both paths and would need to move to a shared static/helper both plugin classes reference. That's more than "move two methods" — it's *split one class into two, extract the shared mapper.*

**Callers that move:** `HouseholdRepository.TryMatchCoLoadedGuardianByBenefitIdAndDobAsync` and `GetHouseholdByBenefitIdentifierAndGuardianDobAsync` (lines 151-213) resolve `ISummerEbtCaseService` today; they'd resolve `ICoLoadCaseService`. `SubmitIdProofingCommandHandler` is a caller too. Small, but real, and spans the portal repo (not just the connector).

**Verdict:** the reshape is the right call and low-risk behaviorally, but "two methods moved" undersells it. Budget for: one-interface-per-class constraint forces a class split, shared mapper extraction, an interface-package bump, DC connector rebuild, and portal-side caller updates. Still worth it — it removes the adapter's only `state=="dc"`-shaped branch.

## Capability derivation reality check

**Works:**
- `AddressUpdate` ← real `IAddressUpdateService` vs `DefaultAddressUpdateService`. DC registers a real one. ✅
- `CardReplacement` ← real `ICardReplacementService`. DC registers a real one. ✅
- `EnrollmentCheck` ← real `IEnrollmentCheckService`. DC registers a real one. ✅
- `CoLoadedLookup` ← real `ICoLoadCaseService` once reshaped. DC registers it, CO doesn't. ✅ — this is exactly the conflation the reshape fixes.

The `TryAddSingleton` default-fallback scheme (loader lines 154-160) is a reliable "not supported" signal: real impl wins via `AddSingleton` before defaults are tried. Good.

**Doesn't work — can't be derived from DI:**
- **`CardDetailsCapability.Modes` (`batch` vs `perCase`).** There's no per-service to inspect. DC is batch-only, but nothing in the container says so. The adapter has to *assert* `modes: [batch]` for any plugin state — a hardcoded fact, contradicting "derived, not asserted."
- **`CardReplacementStatusTracking`.** DC has no status service. Derivation would need a *separate* optional plugin service for status; there isn't one, and inventing one to make the capability derivable is more contract churn than it's worth. So this is asserted `false` for plugins.
- **`UserAssertion`.** Correctly `false` for all plugins (in-process). Fine — but it's asserted, not derived.
- **`ServiceMode`.** No plugin analog.

**Implication:** `PluginAdapter.GetCapabilitiesAsync` is *partly* derived (the four write/co-load booleans) and *partly* a hardcoded plugin baseline (`cardDetails: {supported:true, modes:[batch]}`, `statusTracking:false`, `userAssertion:false`). That's fine and honest, but the TDD should say so rather than implying full derivation.

## Field parity & context threading

### Field parity — better than OQ-1 fears

DC's `MapToCase` already populates nearly the whole "new" field set:

| Spec field | DC source | Status |
|---|---|---|
| `balance` | `EbtCardBalance` | ✅ (spec wants integer cents; DC has `decimal` — **conversion needed**, and units must be confirmed) |
| benefit dates | `BenefitAvailableDate`, `BenefitExpirationDate` | ✅ |
| `eligibilityType` | `EligibilityType` (raw string) | ✅ present, but **free-form DC string**, not the spec enum — mapping/normalization gap |
| `eligibilitySource` | `EligibilitySource` on model | ⚠️ DC `MapToCase` never sets it → always null → adapter omits |
| `displayNumber` | `CaseDisplayNumber` (= `EbtCaseNumber`) | ✅ |
| per-case `mailingAddress` | `MailingAddress` | ✅ (IAL/PII-gated) |
| `isCoLoaded` | `IsCoLoaded` (derived from issuance type) | ✅ |
| `isStreamlineCertified` | `IsStreamlineCertified` (`ApplicationId` empty) | ✅ |
| `applicationId` | `ApplicationId` | ✅ |
| `caseId` | `SummerEBTCaseID` | ✅ |

So OQ-1's "silent display gaps" risk is small for DC — the one true gap is `eligibilitySource` (never populated) and the `balance` units + `eligibilityType` enum-vs-string normalization. Worth a targeted check that no handler treats `eligibilitySource` as required.

### Applications — populated

DC *does* populate `HouseholdData.Applications` (`MapToHouseholdData` lines 444-448, grouped by `ApplicationId`) and sets the case→application link via `SummerEbtCase.ApplicationId`. So the TDD's claim "DC/CO plugins already surface application data" is correct for DC. Note: the connector's `Application` model still carries `BenefitIssueDate`/`BenefitExpirationDate` (the fields the spec moved to the case) — that's OQ-2 tech debt, out of scope, but the adapter maps the model as-is.

### Context threading — **the real hole**

DC's read methods take, explicitly:
- `PiiVisibility` — DC uses `IncludeAddress` to decide whether to map the address at all (line 169), AND gates it on `ial >= IAL1plus`.
- `IdentityAssuranceLevel` — drives `isIdentityProofed`, which the SP takes as `@isIdentityProofed` and which gates address surfacing.
- `portalUserId` — flows into `@guardianIdentifiers` as `PortalUUID` so DC's data team can correlate warehouse rows back to portal users. On the co-load path it's *required* (`Guid`, not `Guid?`).

The new `IStateBackendClient` signatures carry **none** of these. The TDD's model is: `RestStateBackendClient` packs IAL + an opaque `userRef` into the `X-Sebt-User-Identity` JWT; `PluginAdapter` "never sends it — plugins run in-process." But DC's plugin **consumes IAL, PII visibility, and the raw portal user GUID as method arguments** — not a JWT, and `PortalUUID` needs the *actual* `User.Id` GUID, not an HMAC'd `userRef`.

So `PluginAdapter` needs an **ambient request-context source** to reconstruct these before calling DC:
- IAL — from the current `ClaimsPrincipal` (`UserIalLevelExtensions.FromClaimsPrincipal`, as the handlers do today).
- PII visibility — today the *repository* hardcodes "request full PII from plugin, mask in portal layer" (`HouseholdRepository` lines 82-85). The adapter would have to replicate that policy or the portal keeps doing PII masking above the adapter.
- `portalUserId` — the real `User.Id` GUID from claims (`command.User.GetUserId()`), *not* `UserAssertion.UserRef` (which is explicitly a non-reversible HMAC). The JWT abstraction actively *destroys* the value DC needs.

The TDD hasn't specified how the adapter gets this context. Options:
1. `IStateBackendClient` methods take an explicit `RequestContext` (IAL, userId, PII visibility). Cleanest, but changes every signature and touches `RestStateBackendClient` too.
2. Adapter takes `IHttpContextAccessor` and rebuilds context per-call. Works in-process, keeps the interface clean, but hides a dependency and is awkward for non-HTTP callers (background jobs, tests).
3. Keep the current context-carrying repository methods *underneath* the adapter and have the adapter delegate to `IHouseholdRepository` rather than the raw plugin. But that inverts the layering the TDD assumes.

This needs a decision before "mechanical mapping" is even well-defined. **This is the single most under-specified part of the plan.**

## Risks & gaps

1. **`RequestCardReplacementAsync` signature can't express DC's inputs.** *High.* Interface is per-`caseId`, no household identifier, no `CaseRef` triple. DC needs all three. **Implies TDD change** — either the card-replacement op carries household id + case-ref triples (mirror the REST body), or it stays out of `IStateBackendClient`.
2. **`GetCardDetailsAsync(caseId)` has no DC backing and no way to look up by bare caseId.** *High.* DC is batch-only; card data rides the lookup. **Implies TDD change** — spec how a batch-only plugin state satisfies (or refuses) the per-case op; likely "adapter returns null, capability advertises `batch` only, use-case reads from lookup result."
3. **Context threading (IAL / PII visibility / raw portalUserId) unspecified.** *High.* JWT abstraction can't carry the raw `PortalUUID` DC requires, and plugins don't read the JWT anyway. **Implies TDD change** — add an explicit request-context mechanism for the adapter.
4. **`GetHealthAsync` inverts DC's push-registration health model.** *Medium.* DC registers a check at startup; interface wants pull. Adapter must resolve `HealthCheckService` and run the DC-tagged check. **Implies TDD note**, not necessarily a contract change.
5. **Capabilities only partly derivable.** *Medium.* `cardDetails.modes`, `statusTracking`, `userAssertion` can't come from DI — must be an asserted plugin baseline. **Implies TDD wording change** ("derived where a service exists; asserted baseline otherwise").
6. **Co-load reshape is a class split + shared-mapper extraction, not "two methods."** *Low.* Loader enforces one service-interface per plugin class (lines 78-89). **Implies TDD scoping correction**, low risk.
7. **`eligibilitySource` never populated by DC; `balance` units + `eligibilityType` enum normalization.** *Low.* Adapter omits `eligibilitySource`; confirm no handler treats it as required. Convert `decimal` balance to integer cents and confirm DC's units. **OQ-1 refinement.**
8. **Primary-lookup signal dispatch silently returns null for signal types DC doesn't support (phone, benefit id).** *Low.* No capability distinguishes "DC only does email primary lookup," so a wrong signal looks like "not found." Acceptable if the portal only ever sends `email` for DC primary lookups — confirm.
9. **Idempotency key is advisory only.** *Low.* TDD already says this; DC's SP does no dedup, cooldown lives in the portal DB. No change needed, but the interface's `idempotencyKey` param is inert for DC.

## Recommended TDD tweaks

1. **Redefine the card-replacement op** to carry `HouseholdIdentifierValue` + a list of case-ref triples (matching the REST `POST /cases/{caseId}/card-replacement` body plus household context), OR explicitly scope card replacement *out* of `IStateBackendClient` and keep it on the existing `ICardReplacementService` path. Don't ship the per-single-`caseId` signature — it can't round-trip DC.
2. **Add an explicit request-context contract** (IAL, raw portal user id, PII visibility) that both clients consume: `RestStateBackendClient` folds it into the JWT + headers; `PluginAdapter` passes it straight into DC's method args. Resolve the "JWT userRef is HMAC'd but DC needs the raw GUID" conflict head-on.
3. **Say plainly that `PluginAdapter.GetCapabilitiesAsync` is hybrid:** four booleans derived from DI, the rest an asserted plugin baseline. Keep "derived" for the write/co-load capabilities only.
4. **Specify `GetCardDetailsAsync` behavior for batch-only plugin states** — adapter returns null / not-supported, capability advertises `batch`, use-case sources card data from the lookup result.
5. **Note the health-model inversion:** adapter runs the DC-registered check via `HealthCheckService` and maps `HealthReport` → `StateHealth`; DC's service doesn't return health on demand.
6. **Correct the co-load reshape scope:** class split (one service-interface per plugin class), shared-mapper extraction, interface bump, connector rebuild, portal caller updates.

## Open questions

- OQ-A: Does the portal ever send a non-email `Primary` signal for DC? If not, gap #8 is a non-issue. If yes (any future SSO/phone path), the adapter needs an explicit unsupported-signal outcome.
- OQ-B: Is `EbtCardBalance` in dollars or cents at DC's source? Spec wants integer cents. TBD — confirm with DC data team before the adapter converts.
- OQ-C: Does any use-case read `eligibilitySource`? DC never sets it. If a handler assumes presence, it breaks silently.
- OQ-D: Preferred context-threading option (explicit `RequestContext` arg vs `IHttpContextAccessor` vs delegate-to-repository)? This gates whether the interface signatures are final.
- OQ-E: Should card replacement even live on `IStateBackendClient` given the signature mismatch, or stay on the current plugin-service path until a REST middleware exists? TBD — depends on how much the interface is meant to be *the* surface vs. reads-plus-address only.
