# CO CBMS Adapter POC — Trial Planning

Trial planning for DC-569: a standalone REST middleware that speaks the SEBT state-backend contract
(`docs/openapi.yaml`) on the front and Colorado's CBMS SEBT API on the back. This is a paper exercise
to surface risks — no adapter is being built here.

Grounded in the CO connector repo (`sebt-self-service-portal-co-connector`), which already integrates
CBMS via a Kiota-generated client (ADR-0002 there). The CBMS shapes and mapping logic below are taken
from that connector's code and its embedded mock fixtures — the same wire format the adapter would face.

## Summary

- **CBMS is application-centric and flat.** `get-account-details` returns one array, `stdntEnrollDtls[]`,
  with one row per child. There are no separate "case" and "application" entities. The connector
  *synthesizes* both from the same rows by grouping on `sebtAppId` and branching on `eligSrc`. The
  adapter must reimplement this disaggregation; it's derived, not authoritative.
- **CBMS lookup is keyed by guardian phone only.** `get-account-details` takes `phnNm` and nothing else.
  The connector's email/benefit-id/DOB lookups all return `null`/`false`. The contract's arbitrary-signal
  `POST /households/lookup` collapses to "phone or nothing" for CO. `intent: coLoad` is unsupported.
- **No end-user identity, ever.** CBMS auth is OAuth2 client-credentials (service-to-service). Nothing
  validates a per-user JWT. `userAssertion.supported: false`; the adapter ignores `X-Sebt-User-Identity`.
- **Card replacement has no request id, status, or idempotency.** It's the *same* `PATCH /sebt/update-std-dtls`
  call as address update, with `reqNewCard="Y"`. Response is a single `respCd`/`respMsg`. No `requestId`
  to return, no polling endpoint, no dedup key. The adapter must fabricate a `requestId` and owns idempotency
  itself or drops `statusTracking`.
- **Atomic batch address update is not guaranteed by CBMS.** The PATCH takes an array but returns one
  aggregate `respCd` — no per-student result, no documented rollback. The adapter can't honestly promise
  all-or-nothing or populate `failedCases` without its own coordination logic.
- **The one read call is slow (8–9s avg, 30s+ observed) and has unverified read-after-write consistency.**
  `get-account-details` is a POST, not a cheap GET. The CO connector caches it (SWR) and does read-before-write
  to recover CBMS write ids. The adapter inherits both problems: it must cache, and it can't assume a write is
  immediately visible on the next read.

## What CO actually exposes

CBMS SEBT API surface (from the connector's generated client and service calls):

| CBMS operation | Method | Keyed by | Purpose |
|---|---|---|---|
| `get-account-details` | POST | `phnNm` (guardian phone), `ebtCardService=Y/N` query | Household + all children's enrollment rows. **Slow: 8–9s avg, 30s+ observed.** |
| `check-enrollment` | POST | name + DOB + school code rows | Eligibility check, returns match confidence |
| `update-std-dtls` | PATCH | array of `{sebtChldId, sebtAppId, addr, reqNewCard, …}` | Address update AND card replacement (same call) |
| `ping` | — | — | Health probe |
| token endpoint | POST | Basic client_id:secret | OAuth2 client-credentials grant |

**`get-account-details` row (`stdntEnrollDtls[]`) real fields** (from `get-account-details.actual.json`):

`gurdFstNm`, `gurdLstNm`, `gurdPhnNm`, `gurdEmailAddr`, `sebtYear`, `sebtAppId`, `stdFstNm`, `stdLstNm`,
`stdDob`, `stdntEligSts`, `sebtAppSts`, `eligSrc`, `sebtChldId`, `sebtChldCwin`, `addrLn1`, `addrLn2`,
`cty`, `staCd`, `zip`, `zip4`, `ebtCardLastFour`, `benAvalDt`, `benExpDt`, `ebtCardSts`, `cardIssDt`,
`cardBal`, `cbmsCsId`, `dircEligSrc`.

**In the real UAT fixture, every card and benefit field is empty** (`ebtCardLastFour: ""`, `ebtCardSts: ""`,
`cardIssDt: ""`, `cardBal: 0`, `benAvalDt: ""`, `benExpDt: ""`), and `cbmsCsId: ""` is empty too. Card and
benefit data are plumbed in the schema but not populated in practice for the accounts observed.

**How the connector derives cases and applications** (`CbmsResponseMapper.cs`):

- **`caseId` ← `sebtChldCwin`** (child's cross-year CWIN). Stable per child.
- **`applicationId` ← `sebtAppId`**, but only when `eligSrc ∈ {CBMS, PK}` (application-based). Auto-issued
  rows (`eligSrc ∈ {DIRC, CDE}`) get `applicationId = null`.
- **`displayNumber` ← `sebtAppId` if present, else `cbmsCsId`.** Since `cbmsCsId` is empty in real data,
  it's effectively always the app id.
- **`isStreamlineCertified` ← `!IsApplicationBased(eligSrc)`.** A *guess* from eligibility source, not a
  CBMS field.
- **Applications are a group-by projection:** application-based rows grouped by `sebtAppId`, each group
  becomes one `Application` with its children. Same rows feed both `cases` and `applications`.
- **`eligibilityType` ← `stdntEligSts`** (raw 2-letter code, `DD`/`AP`/etc., passed through).
- **Denied-duplicate rows (`stdntEligSts="DD"`) are filtered out** before mapping and before any write
  (`CbmsCaseFilters.IsDeniedDuplicate`).
- Card fields map straight through (`ebtCardSts` → `CardStatus` via a token table; unknown → `Unknown`),
  so in practice they resolve to `Unknown`/null given empty source data.

**Auth to CBMS:** OAuth2 client-credentials. Token endpoint gets HTTP Basic `client_id:client_secret`,
`grant_type=client_credentials`; the bearer token is cached and refreshed 60s before expiry
(`ClientCredentialsTokenProvider.cs`). Config knobs: `Cbms:ClientId`, `Cbms:ClientSecret`,
`Cbms:ApiBaseUrl`, `Cbms:TokenEndpointUrl` (or `Cbms__*` env vars). `ColoradoAuthenticationService`
only decorates Swagger with a Bearer scheme — it does **not** authenticate anything at runtime.

**Enrollment check:** takes child name + DOB + school code, returns `mtchCnfd` (0–100 match confidence)
and `sebtEligSts` (`Y`/`N`). The connector defaults to a >90 confidence gate and does month/day
DOB-transposition retries. No `state_benefit_id` path — CBMS matches on name/DOB.

**Co-loading:** unsupported. `TryMatchCoLoadedGuardianByBenefitIdAndDobAsync` returns `false`; the code
comment states warehouse IC+DOB matching is DC-only.

**Health:** `ping`-based; degrades to `Degraded` when credentials are absent.

## Mapping the contract to CO

Per capability, what the adapter can declare and what it must do.

### `GET /capabilities`

Realistic CO capability document:

```
cases:
  coLoadedLookup:    { supported: false }
  cardDetails:       { supported: true, modes: [batch] }   # data present in schema; often empty
  cardReplacement:   { supported: true, statusTracking: { supported: false } }
  addressUpdate:     { supported: true }
  cardActivation:    { supported: false }
enrollment:
  check:             { supported: true }
userAssertion:       { supported: false }
```

### `POST /households/lookup`

- **Signal support is narrow.** CBMS keys on `phnNm`. The adapter uses the `phone_number` signal and
  ignores everything else. If no phone signal is present, it can only return empty `cases`/`applications`.
  Email, `state_benefit_id`, name/DOB are not lookup keys against `get-account-details`.
- **Disaggregation** reimplements `CbmsResponseMapper`: filter `DD`, map each row to a case
  (`caseId ← sebtChldCwin`), set `applicationId` only for application-based rows, and group
  application-based rows by `sebtAppId` into `applications[]`.
- **`intent: coLoad` → `400` (or route absent)**, since `coLoadedLookup.supported: false`.

### `GET /cases/{caseId}/card` and batch card details

- CBMS carries card data inline on each student row, so `batch` mode is the natural fit — populate
  `cardDetails` on each case during lookup. But the fields are commonly empty, so `cardDetails` will
  frequently be absent/`Unknown` even for issued cards.
- `perCase` gains nothing — there's no per-child card endpoint; it would just re-fetch the phone-keyed
  account. Declare `modes: [batch]` only.

### `POST /cases/address-updates`

- Contract sends `caseIds[]` + one address. CBMS wants a phone-resolved account, then a PATCH array of
  per-student payloads (`sebtChldId`, `sebtAppId`, `addr`). **The adapter has no phone from a `caseIds`-only
  request** — the contract dropped the household identifier. The adapter must re-resolve the household
  (from what? see risk 6) to turn `caseIds` back into CBMS student rows.
- **Atomicity is aspirational.** `UpdateStudentDetailsResponse` is just `{respCd, respMsg}`. No per-student
  status, no rollback contract. The adapter can map a non-success `respCd` to `400`/`409` but can't
  truthfully guarantee all-or-nothing or fill `failedCases`.

### `POST /cases/{caseId}/card-replacement` + status

- Same `PATCH /sebt/update-std-dtls` with `reqNewCard="Y"`. No CBMS `requestId`, no status lifecycle,
  no idempotency.
- The adapter must **synthesize a `requestId`** (contract requires one) and **own the 24h idempotency
  dedup** itself — CBMS won't dedup replays.
- **`statusTracking.supported: false`** — there's nothing to poll. `submitted` is the only honest status.

### `POST /enrollment/check`

- Cleanest fit. Map `child_first_name`/`child_last_name`/`date_of_birth` signals to CBMS check-enrollment
  rows; map `mtchCnfd > threshold && sebtEligSts=="Y"` to `eligible: true`. School code isn't in the
  contract's signal vocabulary — either omit it or add a signal type.

### `GET /health`

- Wrap CBMS `ping` + token acquisition. `pass`/`fail` with `cmsReachable`. Straightforward.

## Risks & gaps

1. **Household lookup is phone-only, but the contract implies arbitrary signals.** *(High. Spec/TDD note.)*
   CBMS `get-account-details` takes `phnNm` only; the connector's email/benefit-id lookups are stubs
   returning null. Any portal flow that lands a CO user without a phone signal cannot resolve a household.
   The contract already says backends use the subset they support and ignore the rest — so this is
   *legal*, but the portal must be prepared for empty results when it only has an email. Worth documenting
   explicitly in the TDD that phone is CO's sole lookup key.

2. **Cases and applications are the same rows, disaggregated by heuristic.** *(High. No spec change; document.)*
   `applicationId`/`isStreamlineCertified` are derived from `eligSrc`, not asserted by CBMS. Applications
   are a `group-by sebtAppId` projection of the case rows. The linkage is internally consistent (same
   `sebtAppId` on both sides) but lossy: an "application" has no independent `submittedDate`/`decisionDate`
   source (see risk 5), and the case/application split depends entirely on the `eligSrc` classifier's four
   known values plus a fallback. Unknown `eligSrc` is treated as auto-eligible. Any new CBMS `eligSrc` value
   silently changes the split.

3. **Card replacement has no request id / status / idempotency; the adapter must own all three.** *(High. Spec/TDD note.)*
   CBMS returns `{respCd, respMsg}`. The contract requires a `requestId`, an `Idempotency-Key` 24h dedup
   window, and (optionally) status polling. The adapter needs **its own state store** to mint request ids
   and dedup replays. Recommend the POC declares `statusTracking.supported: false` and documents that
   `requestId` is adapter-generated and opaque (not resolvable back to CBMS).

4. **Atomic address update can't be honestly guaranteed.** *(Med-High. Spec/TDD note.)*
   The single-`respCd` PATCH response gives no per-case outcome. The contract's all-or-nothing + `failedCases`
   model can't be faithfully implemented on top of CBMS. Either (a) the adapter treats the array PATCH as
   atomic on faith and maps any failure to a blanket `400`/`409` with no `failedCases` detail, or (b) it
   sequences per-student PATCHes and attempts compensating writes on partial failure — but CBMS offers no
   rollback primitive, so (b) can still leave partial state. Recommend documenting that CO returns
   all-or-nothing at the granularity CBMS provides (whole PATCH) and omits `failedCases`.

5. **Several contract case/application fields have no CBMS source.** *(Med. No spec change; adapter omits.)*
   Per the "absent = not applicable" convention, the adapter omits these rather than guessing:
   - `Application.submittedDate` / `decisionDate` — **no CBMS field.** CBMS has `sebtAppSts` (processing
     status codes) but no dates. Omit both.
   - `SummerEbtCase.eligibilitySource` — the connector leaves it unset today; `dircEligSrc`/`eligSrc` exist
     but aren't mapped to it. Could pass through `eligSrc` if the portal wants it.
   - `benefitAvailableAt` / `benefitExpiresAt`, `cardDetails.balance`/`lastFour`/`status`/dates,
     `mailingAddress` — present in schema, **empty in observed data.** Adapter maps what's there; expect
     mostly absent.
   - `isCoLoaded` — the connector hardcodes `BenefitIssuanceType.SummerEbt` and never sets co-loaded.
     Effectively always `false` for CO.

6. **Address/card-replacement need a household identifier the contract doesn't carry on the write.** *(Med. Spec/TDD note.)*
   CBMS writes require resolving the phone-keyed account first to turn portal `caseId`s
   (`sebtChldCwin`) into CBMS `sebtChldId`/`sebtAppId`. The contract's `POST /cases/address-updates`
   and card-replacement bodies carry only `caseId`s, no phone. The connector gets the phone from the
   portal's `HouseholdIdentifierValue`; the REST adapter won't have it. Options: (a) the adapter keeps a
   short-lived caseId→phone map from the preceding lookup, or (b) the contract needs a way to pass the
   resolving identifier on writes. **This is the sharpest spec gap** — flag for discussion.

7. **`caseId` (`sebtChldCwin`) and write ids (`sebtChldId`/`sebtAppId`) are different identifiers.** *(Med. Document.)*
   The portal treats `caseId` as opaque and echoes it back on writes. CO's write path needs the *per-year*
   `sebtChldId`/`sebtAppId`, not the cross-year `sebtChldCwin`. The adapter must re-resolve the row to get
   the write ids (ties into risk 6). The connector already does this via `CbmsGetAccountStudentDetailIds`.

8. **`eligSrc` classifier is a four-value allowlist with a silent fallback.** *(Low-Med. Document/monitor.)*
   `{CBMS, PK}` = application-based, `{DIRC, CDE}` = streamlined, anything else → treated as auto-eligible.
   A new CBMS source code silently flips a child from application-based to auto-issued (dropping its
   `applicationId` and its `applications[]` entry). Needs the same drift alerting the connector's
   `AdditionalData` serialization tests provide.

9. **No end-user identity plumbing.** *(Low. Confirms spec.)* `userAssertion.supported: false`; the adapter
   ignores `X-Sebt-User-Identity`. The middleware authenticates to CBMS purely as a service
   (client-credentials). This matches the contract's design — noted for completeness, not a gap.

10. **Portal↔middleware auth maps cleanly; middleware↔CBMS is a separate credential.** *(Low.)*
    The contract's `oauth2ClientCredentials`/`apiKey` service-auth is what the middleware presents *to the
    portal*. Behind it, the middleware holds CBMS client-credentials (Basic-auth token endpoint → Bearer).
    Two independent credential domains — fine, but the POC must not conflate them (don't pass the portal's
    token through to CBMS).

11. **Slow read + unverified write visibility drive the caching design.** *(Med. No spec change; document.)*
    `get-account-details` at 8–30s makes the capabilities pre-flight and every lookup expensive. The
    connector already runs an SWR cache (soft 15m / hard 4h, negative 60s, stampede coalescing) and does
    read-before-write. The adapter must do the same, and — because CBMS read-after-write consistency is not
    documented — write-through the cache on address update (as the connector does) rather than trusting a
    re-read. Card replacement deliberately does not write through (cooldown lives portal-side).

12. **The enrollment verdict `sebtEligSts` is not in the CBMS typed schema.** *(Low-Med. Document/monitor.)*
    The `Y`/`N` field that actually decides eligibility survives only via Kiota's `AdditionalData` on the
    check-enrollment response. A transform/regeneration that "cleans up" unmapped fields would silently break
    matching. The adapter must read it defensively and guard it with a drift test.

## Recommended spec/TDD tweaks

1. **Add a resolving-identifier path for writes.** Address update and card replacement need the household
   key (phone, for CO) to map `caseId`s to CBMS write ids. Either document that the adapter caches
   caseId→identifier from the preceding lookup (with a TTL and a defined miss behavior — likely `409`/`400`
   "refresh and retry"), or add an optional household-identifier field to the write request bodies. Decide
   before building. This is the one gap that can block the write paths.

2. **Document `requestId` as adapter-minted and `statusTracking` as CO-unsupported.** State in the TDD that
   for CBMS-style backends the `requestId` is opaque to the upstream CMS and idempotency/dedup is the
   adapter's responsibility, backed by its own store. Confirm the 24h window is measured by the adapter.

3. **Loosen the atomic-address-update guarantee to "atomic at the CMS's granularity."** The all-or-nothing
   language assumes the CMS supports transactional multi-case writes with per-case failure reporting. CBMS
   does not. Add a note that backends unable to report per-case outcomes may omit `failedCases` and return
   a single `400`/`409` for the whole batch, and that atomicity is best-effort at whatever unit the CMS
   PATCH provides.

4. **Add `school_code` (or similar) to the enrollment-check signal vocabulary, or note it as optional.**
   CBMS check-enrollment uses school code as a matching input. Today it has no signal type.

5. **Note that `submittedDate`/`decisionDate` may be absent for application-centric backends.** CBMS has
   no application dates. Reassure implementers that omitting them is correct, not a mapping bug.

6. **Call out phone-only lookup as an expected, legal degenerate case.** Document that some backends
   resolve households by a single identifier type and the portal must handle empty results for
   unsupported signals gracefully.

## Open questions

- Where does the caseId→phone (household identifier) mapping live for CO writes — adapter cache, or a new
  request field in the contract? TBD, and it gates the write paths.
- Do real CBMS accounts ever populate card/benefit fields, or are they always empty for the SEBT program?
  If always empty, `cardDetails.supported` and `benefitAvailableAt`/`benefitExpiresAt` are theater — decide
  whether to advertise them at all. TBD (needs CDHS/CBMS confirmation).
- Does CBMS `update-std-dtls` treat its array body atomically, and how does it report partial failure? The
  connector assumes success on a single `respCd` and has no partial-failure handling. TBD (needs CBMS behavior
  confirmation under a deliberately-failing multi-student PATCH).
- Should the adapter reuse the connector's caching layer (the connector caches `get-account-details` per
  phone via HybridCache), or is that the portal's concern now? Caching the phone-keyed account is what makes
  the write-path re-resolution cheap. TBD.
- Is there any CBMS card-replacement confirmation/tracking channel at all (batch file, separate query), or
  is fire-and-forget the ceiling? If tracking exists out-of-band, `statusTracking` could later become `true`.
  TBD.
