# DC-568 Spike — Analysis & Synthesis

Trial-planning and review reports for the state-backend interface generalization spike, plus a synthesis of what they mean for the REST contract (`docs/openapi.yaml`) and the interface TDD (`docs/tdd/state-interface-generalization.md`).

## Reports

- [`co-cbms-adapter-poc-planning.md`](./co-cbms-adapter-poc-planning.md) — trial plan for a REST middleware adapting CO's CBMS-backed API (follow-on POC, DC-569)
- [`plugin-adapter-planning.md`](./plugin-adapter-planning.md) — trial plan for the `PluginAdapter`, stress-tested against DC's current plugin
- [`openapi-implementability-review.md`](./openapi-implementability-review.md) — the spec read through a low-capacity state/vendor implementer's eyes
- [`template-adapter-microservice.md`](./template-adapter-microservice.md) — options for a stubbed template adapter that minimizes implementer effort

## Meta-finding

The contract assumes a backend richer than either real one. Three assumptions broke under stress, and the two backend stress tests hit the same spots independently — which is what makes them trustworthy:

- **Opaque `caseId`s are enough to address writes.** They're not. Both DC and CO need household context to resolve a write back to their internal ids.
- **Backends can do cross-case transactions** (atomic address update). Neither can at that granularity.
- **A card replacement has a native request lifecycle** (`requestId`/status/idempotency). Neither backend has one; the adapter has to fake it.

## Proposed changes, ranked by independent support

| # | Change | Type | Backed by | Recommendation |
|---|---|---|---|---|
| 1 | Write bodies (address update, card replacement) carry an opaque `householdRef` returned by lookup, not just `caseId`s | SPEC | DC + CO | Add it. Stateless, resolves both backends' write-id mapping; opaque, so no PII. |
| 2 | `failedCases` is referenced but never defined as a schema | SPEC (bug) | implementer review | Define it or drop it — decision B |
| 3 | Soften atomic address update to "atomic at the CMS's granularity" | SPEC | CO + implementer + DC | Decision B |
| 4 | Plugin adapter needs an explicit request-context contract (raw `portalUserId`, `PiiVisibility`, IAL) separate from the wire JWT — `userRef`'s HMAC destroys the GUID DC needs | TDD | DC | Fix in TDD. JWT transports context to REST backends; adapter reads raw context from portal session. Two representations. |
| 5 | JWT is the implementer blocker → sell `userAssertion: false` as the default, add a validation appendix, warn on `alg:none`, document `ial` is a decimal not int | DOCS/SPEC | implementer + CO + template | Do it. The template's "we handle JWT for you" is the real mitigation. |
| 6 | Batch-only backends: spec lists `GetCardDetailsAsync(caseId)` + status polling as first-class, but DC has neither | TDD/SPEC | DC | Clarify these are capability-gated; batch-only is a legal shape. |
| 7 | `Application.submittedDate`/`decisionDate` have no CBMS source | SPEC (minor) | CO | Already optional — document that absence is expected. |
| 8 | cases/applications is a synthesized heuristic; linkage may be derived | DOCS | CO | Note it in the spec; not a contract change. |

## Decisions needed before editing

**A — the write path (#1).** Add an opaque `householdRef` to the lookup response and echo it on write bodies? Recommend yes. CO's fallback (adapter-side `caseId`→identifier cache) is stateful and fragile; `householdRef` is stateless and serves both.

**B — atomic address update (#2/#3).** We wanted transactional semantics earlier, but no real backend guarantees cross-case atomicity, and `failedCases` isn't defined. Options:
- *Keep the ideal, allow degradation:* "backends SHOULD roll back; MAY report per-case failures via `failedCases`" + define the schema. (lean)
- *Drop atomicity:* best-effort per-case with a defined result array.

**C — card replacement shape.** DC treats replacement as a household operation targeting multiple case-refs; the spec is per-case (`/cases/{caseId}/card-replacement`). Per-case or per-household-batch? Genuine modeling question — needs a read on how the portal UI drives it. No strong lean.

**D — `intent`: open string vs closed enum.** Implementer review says a closed enum gets implemented correctly far more often; we chose open for extensibility. With two values and a version bump needed to add more anyway, the extensibility benefit is thin, and fail-closed-on-unknown is what we want for an access-control field regardless. Lean closed enum.

## Status

Reports complete. Decisions made and applied to the spec, TDD, and ADR:

- **A — write path:** No `householdRef` contract field. Writes stay keyed on the opaque `caseId`; the adapter resolves it via a self-describing `caseId` (encodes the `CaseRef` triple + household resolver). Plugin case-id uniqueness cleanup noted as follow-up.
- **B — atomic address update:** Kept the ideal, documented degradation ("atomic at the CMS's granularity; SHOULD roll back; MAY report `failedCases`"). Defined the `FailedCase` schema.
- **C — card replacement:** Stays per-case in the contract; added an optional `reason`. The household + `CaseRef`-triple complexity lives in the adapter.
- **D — `intent`:** Changed to a closed enum (`primary`, `coLoad`).
- **End-user assertion (`X-Sebt-User-Identity` / `userAssertion`):** Cut entirely (YAGNI — no state requested it, JWT was the top implementer blocker). Portal stays the authorization authority; IAL is now an internal-only concept. Adapter threads IAL / PII-visibility / raw `portalUserId` from portal session in-process.
- Also: documented batch-only card-details behavior, that `Application.submittedDate`/`decisionDate` may be absent, and that backends may synthesize cases/applications.

Open items now tracked in the TDD's Open Questions (plugin field population, Core `Application` cleanup, `caseId` encoding vs. plugin case-id cleanup).
